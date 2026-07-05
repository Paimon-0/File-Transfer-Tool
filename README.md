# File-Transfer-Tool
# Platform: Windows Server/10/11 IIS 7.0+ & ASP.NET 4.8
A simple tool built by DeepSeek AI for local area network file transfer. Folders unsupported.
There are only imperfect basic functions in this tool, but it is ideal for situations where you can't find USB cables or when you want to send large files quickly.
# How it works
Upload: Files are packaged into an HTTP payload (with a maximum size limit of 4GB) and sent to server, so it supports mutiple file uploads up to 4GB :(. The server caches the data during the upload process and writes all files to disk after the transfer is fully completed. If an error occurs during the process, the upload will fail entirely or only part of the files will be successfully uploaded. The tool doesn't support resumable uploads or file validity verification, so please avoid interruptions and network unstability during the process.
Download: The server just scans the specific folders and writes an "a href" link for each file.
Delete: The client sends parameters to the server and server removes the corresponding file. Only deletions to files in a specific folder are allowed.
