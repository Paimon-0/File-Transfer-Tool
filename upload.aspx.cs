using System;
using System.IO;
using System.Web;
using System.Web.UI;

public partial class upload : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string uploadGroup = Request.Form["uploadGroup"] ?? "group1";
        
        // 根据选择确定上传目录
        string uploadDir;
        if (uploadGroup == "group1")
            uploadDir = Server.MapPath("Files/group1");
        else if (uploadGroup == "group2")
            uploadDir = Server.MapPath("Files/group2");
        else if (uploadGroup == "group3")
            uploadDir = Server.MapPath("Files/group3");
        else
            uploadDir = Server.MapPath("Files/group1");
        
        // 确保上传目录存在
        if (!Directory.Exists(uploadDir))
        {
            Directory.CreateDirectory(uploadDir);
        }
        
        // 检查是否有文件上传
        if (Request.Files.Count > 0)
        {
            bool hasError = false;
            string messageText = "";
            string messageColor = "green";
            
            for (int i = 0; i < Request.Files.Count; i++)
            {
                HttpPostedFile file = Request.Files[i];
                
                if (file.ContentLength > 0)
                {
                    try
                    {
                        // 获取安全的文件名
                        string fileName = Path.GetFileName(file.FileName);
                        
                        // 安全检查：防止路径遍历攻击
                        if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
                        {
                            hasError = true;
                            messageText = "文件名包含非法字符！";
                            messageColor = "red";
                            break;
                        }
                        
                        // 设置保存路径
                        string savePath = Path.Combine(uploadDir, fileName);
                        
                        // 检查文件是否已存在
                        if (File.Exists(savePath))
                        {
                            // 添加时间戳避免冲突
                            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                            string extension = Path.GetExtension(fileName);
                            fileName = nameWithoutExt + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + extension;
                            savePath = Path.Combine(uploadDir, fileName);
                        }
                        
                        // 保存文件
                        file.SaveAs(savePath);
                        
                        messageText += "文件 '" + fileName + "' 上传成功！<br/>";
                        
                        // 记录文件信息
                        FileInfo savedFile = new FileInfo(savePath);
                        messageText += "大小：" + FormatFileSize(savedFile.Length) + "，类型：" + file.ContentType + "<br/><br/>";
                    }
                    catch (Exception ex)
                    {
                        hasError = true;
                        messageText = "上传失败：" + ex.Message;
                        messageColor = "red";
                        break;
                    }
                }
            }
            
            if (!hasError && string.IsNullOrEmpty(messageText))
            {
                messageText = "请选择有效的文件！";
                messageColor = "orange";
            }
            
            // 显示消息
            Response.Write("<html><body style='font-family:Arial;padding:20px;'>");
            Response.Write("<h2>上传结果</h2>");
            Response.Write("<a href='default.aspx'>返回文件列表</a><br/><br/>");
            Response.Write("<div style='color:" + messageColor + ";padding:10px;border:1px solid #ccc;border-radius:5px;'>");
            Response.Write(messageText);
            Response.Write("</div>");
            Response.Write("</body></html>");
            Response.End();
        }
        else
        {
            Response.Write("<html><body style='font-family:Arial;padding:20px;'>");
            Response.Write("<h2>上传结果</h2>");
            Response.Write("<a href='default.aspx'>返回文件列表</a><br/><br/>");
            Response.Write("<div style='color:orange;padding:10px;border:1px solid #ccc;border-radius:5px;'>");
            Response.Write("没有选择要上传的文件！");
            Response.Write("</div>");
            Response.Write("</body></html>");
            Response.End();
        }
    }
    
    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return bytes + " B";
        else if (bytes < 1024 * 1024)
            return (bytes / 1024) + " KB";
        else if (bytes < 1024 * 1024 * 1024)
            return (bytes / (1024 * 1024)) + " MB";
        else
            return (bytes / (1024 * 1024 * 1024)) + " GB";
    }
}