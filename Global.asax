<%@ Application Language="C#" %>
<%@ Import Namespace="System" %>
<%@ Import Namespace="System.Threading" %>

<script runat="server">
    private static Timer tempCleanupTimer;

    protected void Application_Start(object sender, EventArgs e)
    {
        TimeSpan interval = TransferUtility.GetTempCleanupInterval();
        tempCleanupTimer = new Timer(delegate
        {
            try
            {
                TransferUtility.CleanupExpiredTempUploads(TransferUtility.GetTempUploadMaxAge());
            }
            catch
            {
                // Cleanup is best-effort; transfer requests should not fail because maintenance failed.
            }
        }, null, TimeSpan.Zero, interval);
    }

    protected void Application_End(object sender, EventArgs e)
    {
        Timer timer = tempCleanupTimer;
        if (timer != null)
        {
            timer.Dispose();
            tempCleanupTimer = null;
        }
    }
</script>
