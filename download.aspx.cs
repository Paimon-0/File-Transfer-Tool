using System;
using System.Globalization;
using System.IO;
using System.Web;
using System.Web.UI;

public partial class download : Page
{
    private const int StreamBufferBytes = 1024 * 1024;

    protected void Page_Load(object sender, EventArgs e)
    {
        Server.ScriptTimeout = 43200;
        TransferUtility.AddSecurityHeaders(Response);
        Response.TrySkipIisCustomErrors = true;

        bool isHead = String.Equals(Request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase);
        if (!String.Equals(Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase) && !isHead)
        {
            WriteText(405, "Only GET and HEAD are allowed.");
            return;
        }

        if (!TransferSecurity.RequireAuthorized(Request, Response))
        {
            return;
        }

        try
        {
            string group = TransferUtility.NormalizeGroup(Request.QueryString["group"]);
            string fileName = TransferUtility.SanitizeFileName(Request.QueryString["file"]);
            string filePath = TransferUtility.GetExistingFilePath(group, fileName);
            string inlineValue = Request.QueryString["inline"];
            bool openInline = String.Equals(inlineValue, "1", StringComparison.Ordinal);

            if (!String.IsNullOrEmpty(inlineValue) && !openInline)
            {
                WriteText(400, "Invalid inline option.");
                return;
            }

            if (!File.Exists(filePath))
            {
                WriteText(404, "File not found.");
                return;
            }

            FileInfo file = new FileInfo(filePath);
            string rangeHeader = Request.Headers["Range"];
            ByteRange range;

            Response.ContentType = MimeMapping.GetMimeMapping(file.Name);
            Response.AddHeader("Accept-Ranges", "bytes");
            Response.AddHeader("Content-Disposition", BuildContentDisposition(file.Name, openInline));
            if (openInline)
            {
                Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'; style-src 'unsafe-inline'; img-src data: blob:";
            }
            Response.AddHeader("Last-Modified", file.LastWriteTimeUtc.ToString("R", CultureInfo.InvariantCulture));
            Response.AddHeader("ETag", "\"" + file.Length.ToString(CultureInfo.InvariantCulture) + "-" + file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture) + "\"");

            if (!String.IsNullOrEmpty(rangeHeader))
            {
                if (!TryParseRange(rangeHeader, file.Length, out range))
                {
                    Response.StatusCode = 416;
                    Response.AddHeader("Content-Range", "bytes */" + file.Length.ToString(CultureInfo.InvariantCulture));
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }

                Response.StatusCode = 206;
                Response.AddHeader("Content-Range", "bytes " + range.Start.ToString(CultureInfo.InvariantCulture) + "-" + range.End.ToString(CultureInfo.InvariantCulture) + "/" + file.Length.ToString(CultureInfo.InvariantCulture));
                Response.AddHeader("Content-Length", range.Length.ToString(CultureInfo.InvariantCulture));

                if (!isHead)
                {
                    WriteRange(filePath, range);
                }

                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            Response.StatusCode = 200;
            Response.AddHeader("Content-Length", file.Length.ToString(CultureInfo.InvariantCulture));

            if (!isHead)
            {
                Response.TransmitFile(filePath);
            }

            Context.ApplicationInstance.CompleteRequest();
        }
        catch (InvalidOperationException ex)
        {
            WriteText(400, ex.Message);
        }
        catch (Exception)
        {
            WriteText(500, "Download failed due to a server error.");
        }
    }

    private void WriteRange(string filePath, ByteRange range)
    {
        Response.BufferOutput = false;
        byte[] buffer = new byte[StreamBufferBytes];
        long remaining = range.Length;

        using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, StreamBufferBytes, FileOptions.SequentialScan))
        {
            stream.Seek(range.Start, SeekOrigin.Begin);

            while (remaining > 0 && Response.IsClientConnected)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read = stream.Read(buffer, 0, toRead);
                if (read <= 0)
                {
                    break;
                }

                Response.OutputStream.Write(buffer, 0, read);
                remaining -= read;
            }
        }
    }

    private static bool TryParseRange(string rangeHeader, long fileLength, out ByteRange range)
    {
        range = new ByteRange();
        if (fileLength < 0 || String.IsNullOrWhiteSpace(rangeHeader))
        {
            return false;
        }

        if (!rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase) || rangeHeader.IndexOf(',') >= 0)
        {
            return false;
        }

        string spec = rangeHeader.Substring(6).Trim();
        string[] parts = spec.Split('-');
        if (parts.Length != 2)
        {
            return false;
        }

        long start;
        long end;

        if (String.IsNullOrWhiteSpace(parts[0]))
        {
            long suffixLength;
            if (!Int64.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out suffixLength) || suffixLength <= 0)
            {
                return false;
            }

            start = Math.Max(0, fileLength - suffixLength);
            end = fileLength - 1;
        }
        else
        {
            if (!Int64.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out start))
            {
                return false;
            }

            if (String.IsNullOrWhiteSpace(parts[1]))
            {
                end = fileLength - 1;
            }
            else if (!Int64.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out end))
            {
                return false;
            }
        }

        if (fileLength == 0 || start < 0 || end < start || start >= fileLength)
        {
            return false;
        }

        end = Math.Min(end, fileLength - 1);
        range.Start = start;
        range.End = end;
        range.Length = end - start + 1;
        return true;
    }

    private static string BuildContentDisposition(string fileName, bool inline)
    {
        string fallback = fileName.Replace("\\", "_").Replace("/", "_").Replace("\"", "'");
        return (inline ? "inline" : "attachment") + "; filename=\"" + fallback + "\"; filename*=UTF-8''" + TransferUtility.Url(fileName);
    }

    private void WriteText(int statusCode, string message)
    {
        Response.StatusCode = statusCode;
        Response.ContentType = "text/plain";
        Response.Write(message);
        Context.ApplicationInstance.CompleteRequest();
    }

    private class ByteRange
    {
        public long Start;
        public long End;
        public long Length;
    }
}
