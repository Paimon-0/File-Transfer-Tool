<%@ Page Language="C#" %>
<%@ Import Namespace="System.IO" %>

<!DOCTYPE html>
<html>
<head>
    <title>局域网文件共享</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px; }
        .file-item { 
            padding: 10px; 
            border-bottom: 1px solid #eee; 
            margin: 5px 0;
        }
        .file-item:hover { background-color: #f9f9f9; }
        .upload-form { 
            background: #f5f5f5; 
            padding: 20px; 
            border-radius: 5px; 
            margin-bottom: 30px;
        }
        .file-size { color: #666; margin-left: 10px; }
        .group-select { margin-bottom: 15px; }
        .group-select label { margin-right: 15px; }
        .private-notice { color: #999; font-size: 12px; margin-top: 5px; }
    </style>
</head>
<body>
    <h1>局域网文件共享</h1>
    
    <div class="upload-form">
        <h3>上传文件</h3>
        <form method="post" enctype="multipart/form-data" action="upload.aspx">
            <div class="group-select">
                <strong>选择上传目录：</strong><br/>
                <label><input type="radio" name="uploadGroup" value="group1" checked>可删除</label>
                <label><input type="radio" name="uploadGroup" value="group2">不可删除</label>
                <label><input type="radio" name="uploadGroup" value="group3">不列出</label>
            </div>
            <input type="file" name="file" id="fileInput" multiple>
            <input type="submit" value="开始上传">
            <p><small>支持多文件选择（最大4GB）</small></p>
        </form>
    </div>
    
    <h3>文件列表1</h3>
    <%
        // 获取文件目录
        string Group1Path = Server.MapPath("Files/group1/");
        
        // 如果Files目录不存在，则创建
        if (!Directory.Exists(Group1Path))
        {
            Directory.CreateDirectory(Group1Path);
            Response.Write("<p>暂无文件。</p>");
        }
        else
        {
            DirectoryInfo dir = new DirectoryInfo(Group1Path);
            
            // 显示子目录
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                Response.Write("<div class='file-item'>" + subDir.Name + "/</div>");
            }
            
            // 显示文件
            foreach (FileInfo file in dir.GetFiles())
            {
                string fileSize;
                long fileLength = file.Length;
                
                if (fileLength < 1024)
                    fileSize = fileLength + " B";
                else if (fileLength < 1024 * 1024)
                    fileSize = (fileLength / 1024) + " KB";
                else
                    fileSize = (fileLength / (1024 * 1024)) + " MB";
                
                Response.Write("<div class='file-item'>" + file.Name + 
                              " <span class='file-size'>(" + fileSize + ")</span> " +
                              "<a href='Files/group1/" + file.Name + "' style='margin-left:20px;'>打开</a>" +
                              "<a href='Files/group1/" + file.Name + "' download='" + file.Name +"' style='margin-left:20px;'>下载</a>" +
                              "<a href='javascript:void(0);' onclick='deleteFile(\"" + file.Name + "\", \"group1\")' style='margin-left:10px;color:red;'>删除</a>" +
                              "</div>");
            }
            
            if (dir.GetFiles().Length == 0 && dir.GetDirectories().Length == 0)
            {
                Response.Write("<p>暂无文件。</p>");
            }
        }
    %>
    
    <h3>文件列表2</h3>
    <%
        // 获取文件目录
        string Group2Path = Server.MapPath("Files/group2/");
        
        // 如果Files目录不存在，则创建
        if (!Directory.Exists(Group2Path))
        {
            Directory.CreateDirectory(Group2Path);
            Response.Write("<p>暂无文件。</p>");
        }
        else
        {
            DirectoryInfo dir = new DirectoryInfo(Group2Path);
            
            // 显示子目录
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                Response.Write("<div class='file-item'>" + subDir.Name + "/</div>");
            }
            
            // 显示文件
            foreach (FileInfo file in dir.GetFiles())
            {
                string fileSize;
                long fileLength = file.Length;
                
                if (fileLength < 1024)
                    fileSize = fileLength + " B";
                else if (fileLength < 1024 * 1024)
                    fileSize = (fileLength / 1024) + " KB";
                else
                    fileSize = (fileLength / (1024 * 1024)) + " MB";
                
                Response.Write("<div class='file-item'>" + file.Name + 
                              " <span class='file-size'>(" + fileSize + ")</span> " +
                              "<a href='Files/group2/" + file.Name + "' style='margin-left:20px;'>打开</a>" +
                              "<a href='Files/group2/" + file.Name + "' download='" + file.Name +"' style='margin-left:20px;'>下载</a>" +
                              "</div>");
            }
            
            if (dir.GetFiles().Length == 0 && dir.GetDirectories().Length == 0)
            {
                Response.Write("<p>暂无文件。</p>");
            }
        }
    %>
    
    <script type="text/javascript">
    function deleteFile(filename, fileType) {
        if (confirm('确定要删除 "' + filename + '" 吗？')) {
            var deleteUrl = 'delete.aspx?file=' + encodeURIComponent(filename) + 
                           '&group=' + fileType;
            var xhr = new XMLHttpRequest() || new ActiveXObject("Microsoft.XMLHTTP");
            
            xhr.open('GET', deleteUrl, true);
            xhr.onreadystatechange = function() {
                if (xhr.readyState == 4) {
                    if (xhr.status == 200) {
                        var response = xhr.responseText;
                        
                        if (response.indexOf('SUCCESS:') === 0) {
                            var successMsg = response.substring(8);
                            alert(successMsg);
                            // 刷新页面显示最新文件列表
                            location.reload();
                        } else if (response.indexOf('ERROR:') === 0) {
                            var errorMsg = response.substring(6);
                            alert('删除失败：' + errorMsg);
                        } else {
                            alert('发生什么事了？\n' + response);
                        }
                    } else {
                        alert('请求失败，状态码：' + xhr.status);
                    }
                }
            };
            xhr.send();
        }
    }
    
        // 文件选择时显示文件名
        document.getElementById('fileInput').addEventListener('change', function(e) {
            var files = e.target.files;
            var totalSize = 0;
            
            for (var i = 0; i < files.length; i++) {
                totalSize += files[i].size;
            }
            
            if (files.length > 0) {
                var sizeText = '';
                if (totalSize < 1024)
                    sizeText = totalSize + ' B';
                else if (totalSize < 1024 * 1024)
                    sizeText = (totalSize / 1024).toFixed(1) + ' KB';
                else
                    sizeText = (totalSize / (1024 * 1024)).toFixed(1) + ' MB';
                
                alert('已选择 ' + files.length + ' 个文件，总大小：' + sizeText);
            }
        });
    </script>
</body>
</html>