using System;
using System.IO;
using System.Web;

public partial class delete : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.ContentType = "text/plain";
        
        try
        {
            // 获取参数
            string fileName = Request.QueryString["file"];
            string group = Request.QueryString["group"] ?? "group1";
            
            if (string.IsNullOrEmpty(fileName))
            {
                Response.Write("ERROR:请指定要删除的文件名！");
                return;
            }
            
            // 安全检查
            if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
            {
                Response.Write("ERROR:文件名包含非法字符！");
                return;
            }
            
            // 确定文件路径
            string directory;
            if (group.ToLower() == "group1")
                directory = "Files/group1/";
            else
            {
                Response.Write("ERROR:操作无效");
                return;
            }
            
            string filePath = Server.MapPath(Path.Combine(directory, fileName));
            
            // 检查文件是否存在
            if (!File.Exists(filePath))
            {
                Response.Write("ERROR:文件不存在！");
                return;
            }
            
            // 删除文件
            File.Delete(filePath);
            
            Response.Write("SUCCESS:文件 '" + fileName + "' 删除成功！");
        }
        catch (Exception ex)
        {
            Response.Write("ERROR:删除失败：" + ex.Message);
        }
        
        Response.End();
    }
}