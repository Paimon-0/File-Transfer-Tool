using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.UI;

public partial class upload : Page
{
    private const int CopyBufferBytes = 1024 * 1024;

    protected void Page_Load(object sender, EventArgs e)
    {
        Server.ScriptTimeout = 43200;
        Response.ContentEncoding = System.Text.Encoding.UTF8;
        Response.ContentType = "text/html; charset=utf-8";
        Response.TrySkipIisCustomErrors = true;
        TransferUtility.AddSecurityHeaders(Response);
        TransferUtility.CleanupExpiredTempUploadsIfDue();

        if (!String.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            RenderResult("Upload", new string[] { "Only POST is allowed." }, true);
            return;
        }

        if (!TransferSecurity.RequireAuthorized(Request, Response))
        {
            return;
        }

        List<string> messages = new List<string>();
        bool hasError = false;

        try
        {
            string uploadGroup = TransferUtility.NormalizeGroup(Request.Form["uploadGroup"]);
            long maxFileBytes = TransferUtility.GetMaxFileBytes();

            if (Request.Files.Count == 0)
            {
                RenderResult("Upload", new string[] { "No file was selected." }, true);
                return;
            }

            for (int i = 0; i < Request.Files.Count; i++)
            {
                HttpPostedFile file = Request.Files[i];
                if (file == null || file.ContentLength < 0)
                {
                    continue;
                }

                string fileName = TransferUtility.SanitizeFileName(file.FileName);
                if (maxFileBytes > 0 && file.ContentLength > maxFileBytes)
                {
                    hasError = true;
                    messages.Add(fileName + " is larger than the configured TransferMaxFileBytes limit.");
                    continue;
                }

                string destinationPath = TransferUtility.GetUniqueDestinationPath(uploadGroup, fileName);
                string tempPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".uploading";
                long bytesWritten = 0;

                try
                {
                    using (FileStream output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferBytes, FileOptions.SequentialScan))
                    {
                        bytesWritten = CopyStream(file.InputStream, output);
                        output.Flush(true);
                    }

                    File.Move(tempPath, destinationPath);
                    messages.Add(Path.GetFileName(destinationPath) + " uploaded successfully (" + TransferUtility.FormatFileSize(bytesWritten) + ").");
                }
                catch
                {
                    SafeDelete(tempPath);
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            hasError = true;
            messages.Add("Upload failed: " + ex.Message);
        }

        if (messages.Count == 0)
        {
            hasError = true;
            messages.Add("No valid file was selected.");
        }

        RenderResult("Upload result", messages.ToArray(), hasError);
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

    private void RenderResult(string title, string[] messages, bool hasError)
    {
        Response.Write("<!doctype html><html><head><meta charset=\"utf-8\"><title>");
        Response.Write(TransferUtility.Html(title));
        Response.Write("</title><style>body{font-family:Segoe UI,Arial,sans-serif;margin:40px;line-height:1.5}.box{border:1px solid #d7dde8;border-radius:8px;padding:18px;max-width:760px}.error{color:#b42318}.ok{color:#067647}a{color:#175cd3}</style></head><body>");
        Response.Write("<h1>");
        Response.Write(TransferUtility.Html(title));
        Response.Write("</h1><p><a href=\"default.aspx\">Back to file list</a></p><div class=\"box ");
        Response.Write(hasError ? "error" : "ok");
        Response.Write("\"><ul>");

        for (int i = 0; i < messages.Length; i++)
        {
            Response.Write("<li>");
            Response.Write(TransferUtility.Html(messages[i]));
            Response.Write("</li>");
        }

        Response.Write("</ul></div></body></html>");
        Context.ApplicationInstance.CompleteRequest();
    }
}
