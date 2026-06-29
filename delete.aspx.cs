using System;
using System.IO;
using System.Web.UI;

public partial class delete : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
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
            string group = TransferUtility.NormalizeGroup(Request.Form["group"]);
            if (!String.Equals(group, TransferUtility.GroupDeletable, StringComparison.OrdinalIgnoreCase))
            {
                WriteError(403, "Only files in group1 can be deleted.");
                return;
            }

            string fileName = TransferUtility.SanitizeFileName(Request.Form["file"]);
            string filePath = TransferUtility.GetExistingFilePath(group, fileName);

            if (!File.Exists(filePath))
            {
                WriteError(404, "File not found.");
                return;
            }

            File.Delete(filePath);
            WriteJson(200, "{\"ok\":true,\"message\":\"Deleted " + JsonEscape(fileName) + ".\"}");
        }
        catch (InvalidOperationException ex)
        {
            WriteError(400, ex.Message);
        }
        catch (Exception ex)
        {
            WriteError(500, "Delete failed: " + ex.Message);
        }
    }

    private void WriteError(int statusCode, string message)
    {
        WriteJson(statusCode, "{\"ok\":false,\"error\":\"" + JsonEscape(message) + "\"}");
    }

    private void WriteJson(int statusCode, string json)
    {
        Response.StatusCode = statusCode;
        Response.Write(json);
        Context.ApplicationInstance.CompleteRequest();
    }

    private static string JsonEscape(string value)
    {
        return TransferUtility.JavaScript(value ?? String.Empty);
    }
}
