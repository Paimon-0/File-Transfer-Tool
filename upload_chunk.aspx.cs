using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.UI;

public partial class upload_chunk : Page
{
    private const int CopyBufferBytes = 1024 * 1024;
    private const int MaxChunkCount = 1000000;
    private static readonly object MetadataLock = new object();

    protected void Page_Load(object sender, EventArgs e)
    {
        Server.ScriptTimeout = 43200;
        Response.ContentType = "application/json";
        Response.TrySkipIisCustomErrors = true;
        TransferUtility.AddSecurityHeaders(Response);

        if (!String.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            WriteError(405, "Only POST is allowed.");
            return;
        }

        if (!TransferSecurity.RequireAuthorized(Request, Response))
        {
            return;
        }

        try
        {
            TransferUtility.CleanupExpiredTempUploadsIfDue();

            UploadChunkResult result = SaveChunk();
            WriteJson(200, ResultToJson(result));
        }
        catch (DuplicateFileException ex)
        {
            WriteError(409, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            WriteError(400, ex.Message);
        }
        catch (HttpException ex)
        {
            WriteError(400, ex.Message);
        }
        catch (Exception)
        {
            WriteError(500, "Upload failed due to a server error.");
        }
    }

    private UploadChunkResult SaveChunk()
    {
        string uploadId = TransferUtility.SanitizeUploadId(Request.Form["uploadId"]);
        string group = TransferUtility.NormalizeGroup(Request.Form["group"]);
        string fileName = TransferUtility.SanitizeFileName(Request.Form["fileName"]);
        int chunkIndex = TransferUtility.ParseIntForm(Request, "chunkIndex");
        int totalChunks = TransferUtility.ParseIntForm(Request, "totalChunks");
        long totalSize = TransferUtility.ParseLongForm(Request, "totalSize");
        bool keepDuplicates = TransferUtility.ParseBooleanForm(Request, "keepDuplicates");
        FileTimestampInfo timestamps = ParseClientFileTimes(Request);

        if (totalChunks <= 0 || totalChunks > MaxChunkCount)
        {
            throw new InvalidOperationException("Chunk count is outside the supported range.");
        }
        if (chunkIndex < 0 || chunkIndex >= totalChunks)
        {
            throw new InvalidOperationException("Chunk index is outside the supported range.");
        }
        if (totalSize < 0)
        {
            throw new InvalidOperationException("Total file size cannot be negative.");
        }

        long maxFileBytes = TransferUtility.GetMaxFileBytes();
        if (maxFileBytes > 0 && totalSize > maxFileBytes)
        {
            throw new InvalidOperationException("File is larger than the configured TransferMaxFileBytes limit.");
        }

        HttpPostedFile chunk = Request.Files["chunk"];
        if (chunk == null && Request.Files.Count > 0)
        {
            chunk = Request.Files[0];
        }
        if (chunk == null)
        {
            throw new InvalidOperationException("Chunk file is required.");
        }
        if (chunk.ContentLength > TransferUtility.GetMaxChunkBytes())
        {
            throw new InvalidOperationException("Chunk is larger than the configured TransferMaxChunkBytes limit.");
        }
        if (totalSize > 0 && chunk.ContentLength == 0)
        {
            throw new InvalidOperationException("Chunk is empty.");
        }

        TransferUtility.EnsureDestinationAvailable(group, fileName, keepDuplicates);

        string sessionPath = TransferUtility.GetTempUploadPath(uploadId);
        TransferUtility.TouchTempUploadSession(sessionPath);
        WriteOrValidateMetadata(sessionPath, fileName, group, totalChunks, totalSize, keepDuplicates, timestamps);

        string chunkPath = Path.Combine(sessionPath, GetChunkFileName(chunkIndex));
        string tempChunkPath = chunkPath + "." + Guid.NewGuid().ToString("N") + ".uploading";
        long bytesWritten = SavePostedFile(chunk, tempChunkPath);

        if (bytesWritten != chunk.ContentLength)
        {
            SafeDelete(tempChunkPath);
            throw new IOException("Chunk length changed while saving.");
        }

        if (File.Exists(chunkPath) && new FileInfo(chunkPath).Length == bytesWritten)
        {
            SafeDelete(tempChunkPath);
        }
        else
        {
            if (File.Exists(chunkPath))
            {
                File.Delete(chunkPath);
            }
            File.Move(tempChunkPath, chunkPath);
        }

        TransferUtility.TouchTempUploadSession(sessionPath);

        UploadChunkResult result = new UploadChunkResult();
        result.UploadId = uploadId;
        result.ChunkIndex = chunkIndex;
        result.TotalChunks = totalChunks;
        result.BytesReceived = bytesWritten;
        result.Complete = false;

        if (!AllChunksPresent(sessionPath, totalChunks))
        {
            return result;
        }

        FileStream mergeLock = null;
        if (!TryAcquireMergeLock(sessionPath, out mergeLock))
        {
            result.Merging = true;
            return result;
        }

        using (mergeLock)
        {
            TransferUtility.TouchTempUploadSession(sessionPath);
            MergeResult mergeResult = MergeChunks(sessionPath, group, fileName, totalChunks, totalSize, uploadId, keepDuplicates, timestamps);
            result.Complete = true;
            result.Merging = false;
            result.StoredFileName = Path.GetFileName(mergeResult.FilePath);
            result.StoredSize = mergeResult.Size;
            result.Sha256 = mergeResult.Sha256;
        }

        try
        {
            Directory.Delete(sessionPath, true);
        }
        catch
        {
            // A completed upload should not fail because temporary cleanup had a transient lock.
        }

        return result;
    }

    private static long SavePostedFile(HttpPostedFile postedFile, string destinationPath)
    {
        using (FileStream output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferBytes, FileOptions.SequentialScan))
        {
            return CopyStream(postedFile.InputStream, output);
        }
    }

    private static MergeResult MergeChunks(string sessionPath, string group, string fileName, int totalChunks, long expectedSize, string uploadId, bool keepDuplicates, FileTimestampInfo timestamps)
    {
        string mergePath = Path.Combine(sessionPath, "merged_" + uploadId + ".merging");
        long totalWritten = 0;
        byte[] buffer = new byte[CopyBufferBytes];
        byte[] hash;

        using (SHA256 sha256 = SHA256.Create())
        using (FileStream output = new FileStream(mergePath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferBytes, FileOptions.SequentialScan))
        {
            for (int i = 0; i < totalChunks; i++)
            {
                string chunkPath = Path.Combine(sessionPath, GetChunkFileName(i));
                using (FileStream input = new FileStream(chunkPath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferBytes, FileOptions.SequentialScan))
                {
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, read);
                        sha256.TransformBlock(buffer, 0, read, null, 0);
                        totalWritten += read;
                    }
                }
            }

            sha256.TransformFinalBlock(new byte[0], 0, 0);

            if (totalWritten != expectedSize)
            {
                throw new IOException("Merged file size does not match upload metadata.");
            }

            output.Flush(true);
            hash = sha256.Hash;
        }

        string finalPath = TransferUtility.MoveFileToDestination(mergePath, group, fileName, keepDuplicates, DateTime.Now);
        ApplyClientFileTimes(finalPath, timestamps);

        MergeResult result = new MergeResult();
        result.FilePath = finalPath;
        result.Size = totalWritten;
        result.Sha256 = TransferUtility.ToHex(hash);
        return result;
    }

    private static long CopyStream(Stream input, Stream output)
    {
        byte[] buffer = new byte[CopyBufferBytes];
        long total = 0;
        int read;

        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, read);
            total += read;
        }

        return total;
    }

    private static void WriteOrValidateMetadata(string sessionPath, string fileName, string group, int totalChunks, long totalSize, bool keepDuplicates, FileTimestampInfo timestamps)
    {
        string metadataPath = Path.Combine(sessionPath, "upload.meta");
        string metadata = "group=" + group + "\n" +
                          "fileName=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(fileName)) + "\n" +
                          "totalChunks=" + totalChunks.ToString(CultureInfo.InvariantCulture) + "\n" +
                          "totalSize=" + totalSize.ToString(CultureInfo.InvariantCulture) + "\n" +
                          "keepDuplicates=" + (keepDuplicates ? "true" : "false") + "\n" +
                          "createdUtc=" + FormatTimestamp(timestamps.CreatedUtc) + "\n" +
                          "lastModifiedUtc=" + FormatTimestamp(timestamps.LastModifiedUtc) + "\n" +
                          "lastAccessedUtc=" + FormatTimestamp(timestamps.LastAccessedUtc) + "\n";

        lock (MetadataLock)
        {
            if (!File.Exists(metadataPath))
            {
                File.WriteAllText(metadataPath, metadata, Encoding.UTF8);
                return;
            }

            if (!String.Equals(File.ReadAllText(metadataPath, Encoding.UTF8), metadata, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Upload metadata changed between chunks.");
            }
        }
    }

    private static FileTimestampInfo ParseClientFileTimes(HttpRequest request)
    {
        FileTimestampInfo timestamps = new FileTimestampInfo();
        timestamps.CreatedUtc = ParseOptionalTimestamp(request, "createdUtc", "creationTimeUtc", "created", "creationTime");
        timestamps.LastModifiedUtc = ParseOptionalTimestamp(request, "lastModifiedUtc", "lastModified");
        timestamps.LastAccessedUtc = ParseOptionalTimestamp(request, "lastAccessedUtc", "lastAccessTimeUtc", "lastAccessed", "accessed");
        return timestamps;
    }

    private static DateTime? ParseOptionalTimestamp(HttpRequest request, params string[] fieldNames)
    {
        for (int i = 0; i < fieldNames.Length; i++)
        {
            string raw = request.Form[fieldNames[i]];
            if (!String.IsNullOrWhiteSpace(raw))
            {
                return ParseTimestampValue(raw.Trim(), fieldNames[i]);
            }
        }

        return null;
    }

    private static DateTime ParseTimestampValue(string raw, string fieldName)
    {
        long unixMilliseconds;
        DateTime parsed;

        if (Int64.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out unixMilliseconds))
        {
            try
            {
                parsed = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(unixMilliseconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new InvalidOperationException(fieldName + " is outside the supported timestamp range.");
            }
        }
        else if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
        {
            throw new InvalidOperationException(fieldName + " is not a valid timestamp.");
        }

        parsed = parsed.ToUniversalTime();
        if (parsed < new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc))
        {
            throw new InvalidOperationException(fieldName + " is earlier than the supported file timestamp range.");
        }

        return parsed;
    }

    private static string FormatTimestamp(DateTime? timestamp)
    {
        if (!timestamp.HasValue)
        {
            return "";
        }

        return timestamp.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
    }

    private static void ApplyClientFileTimes(string filePath, FileTimestampInfo timestamps)
    {
        if (timestamps == null)
        {
            return;
        }

        TrySetFileTime(filePath, timestamps.CreatedUtc, File.SetCreationTimeUtc);
        TrySetFileTime(filePath, timestamps.LastAccessedUtc, File.SetLastAccessTimeUtc);
        TrySetFileTime(filePath, timestamps.LastModifiedUtc, File.SetLastWriteTimeUtc);
    }

    private static void TrySetFileTime(string filePath, DateTime? timestamp, Action<string, DateTime> setter)
    {
        if (!timestamp.HasValue)
        {
            return;
        }

        try
        {
            setter(filePath, timestamp.Value.ToUniversalTime());
        }
        catch
        {
            // Timestamp preservation is best effort because filesystems differ in supported metadata.
        }
    }

    private static bool AllChunksPresent(string sessionPath, int totalChunks)
    {
        for (int i = 0; i < totalChunks; i++)
        {
            if (!File.Exists(Path.Combine(sessionPath, GetChunkFileName(i))))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryAcquireMergeLock(string sessionPath, out FileStream mergeLock)
    {
        string lockPath = Path.Combine(sessionPath, "merge.lock");
        try
        {
            mergeLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            mergeLock.SetLength(0);
            return true;
        }
        catch (IOException)
        {
            mergeLock = null;
            return false;
        }
    }

    private static string GetChunkFileName(int chunkIndex)
    {
        return "chunk_" + chunkIndex.ToString("D8", CultureInfo.InvariantCulture) + ".part";
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private void WriteError(int statusCode, string message)
    {
        Response.StatusCode = statusCode;
        WriteJson(statusCode, "{\"ok\":false,\"error\":\"" + JsonEscape(message) + "\"}");
    }

    private void WriteJson(int statusCode, string json)
    {
        Response.StatusCode = statusCode;
        Response.Write(json);
        Context.ApplicationInstance.CompleteRequest();
    }

    private static string ResultToJson(UploadChunkResult result)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("{\"ok\":true");
        builder.Append(",\"uploadId\":\"").Append(JsonEscape(result.UploadId)).Append("\"");
        builder.Append(",\"chunkIndex\":").Append(result.ChunkIndex.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"totalChunks\":").Append(result.TotalChunks.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"bytesReceived\":").Append(result.BytesReceived.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"complete\":").Append(result.Complete ? "true" : "false");
        builder.Append(",\"merging\":").Append(result.Merging ? "true" : "false");

        if (!String.IsNullOrEmpty(result.StoredFileName))
        {
            builder.Append(",\"fileName\":\"").Append(JsonEscape(result.StoredFileName)).Append("\"");
            builder.Append(",\"size\":").Append(result.StoredSize.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"sizeText\":\"").Append(JsonEscape(TransferUtility.FormatFileSize(result.StoredSize))).Append("\"");
            builder.Append(",\"sha256\":\"").Append(JsonEscape(result.Sha256)).Append("\"");
        }

        builder.Append("}");
        return builder.ToString();
    }

    private static string JsonEscape(string value)
    {
        return TransferUtility.JavaScript(value ?? String.Empty);
    }

    private class FileTimestampInfo
    {
        public DateTime? CreatedUtc;
        public DateTime? LastModifiedUtc;
        public DateTime? LastAccessedUtc;
    }

    private class UploadChunkResult
    {
        public string UploadId;
        public int ChunkIndex;
        public int TotalChunks;
        public long BytesReceived;
        public bool Complete;
        public bool Merging;
        public string StoredFileName;
        public long StoredSize;
        public string Sha256;
    }

    private class MergeResult
    {
        public string FilePath;
        public long Size;
        public string Sha256;
    }
}
