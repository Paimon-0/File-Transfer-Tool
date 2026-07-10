<%@ Page Language="C#" %>
<%@ Import Namespace="System" %>
<%@ Import Namespace="System.Globalization" %>
<%@ Import Namespace="System.IO" %>

<script runat="server">
    protected bool IsAuthorized;
    protected bool TokenEnabled;
    protected string RequestToken = "";
    protected int MaxChunkBytes;
    protected string MaxChunkBytesSource = "";
    protected int ParallelUploads;
    protected long MaxFileBytes;

    protected void Page_Load(object sender, EventArgs e)
    {
        Response.ContentEncoding = System.Text.Encoding.UTF8;
        Response.TrySkipIisCustomErrors = true;
        TransferUtility.AddSecurityHeaders(Response);
        TransferUtility.CleanupExpiredTempUploadsIfDue();

        TokenEnabled = TransferSecurity.IsAccessTokenEnabled();
        RequestToken = Request.QueryString["token"] ?? "";
        IsAuthorized = TransferSecurity.IsAuthorized(Request);
        MaxChunkBytes = TransferUtility.GetMaxChunkBytes();
        MaxChunkBytesSource = TransferUtility.GetMaxChunkBytesSource();
        ParallelUploads = TransferUtility.GetClientParallelUploads();
        MaxFileBytes = TransferUtility.GetMaxFileBytes();

        if (IsAuthorized)
        {
            TransferUtility.EnsureDirectory(TransferUtility.GetGroupStoragePath(TransferUtility.GroupDeletable));
            TransferUtility.EnsureDirectory(TransferUtility.GetGroupStoragePath(TransferUtility.GroupReadOnly));
            TransferUtility.EnsureDirectory(TransferUtility.GetGroupStoragePath(TransferUtility.GroupHidden));
        }
    }

    protected string TokenQuery()
    {
        if (String.IsNullOrEmpty(RequestToken))
        {
            return "";
        }

        return "&token=" + TransferUtility.Url(RequestToken);
    }

    protected void RenderFileRows(string group, bool allowDelete)
    {
        string path = TransferUtility.GetGroupStoragePath(group);
        DirectoryInfo directory = new DirectoryInfo(path);
        FileInfo[] files = directory.GetFiles();
        Array.Sort(files, delegate(FileInfo left, FileInfo right)
        {
            return right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc);
        });

        if (files.Length == 0)
        {
            Response.Write("<tr><td colspan=\"6\" class=\"empty\">No files</td></tr>");
            return;
        }

        for (int i = 0; i < files.Length; i++)
        {
            FileInfo file = files[i];
            string fileName = file.Name;
            string downloadUrl = "download.aspx?group=" + TransferUtility.Url(group) + "&file=" + TransferUtility.Url(fileName) + TokenQuery();
            string openUrl = downloadUrl + "&inline=1";

            Response.Write("<tr>");
            Response.Write("<td class=\"name\"><span title=\"");
            Response.Write(TransferUtility.Html(fileName));
            Response.Write("\">");
            Response.Write(TransferUtility.Html(fileName));
            Response.Write("</span></td>");
            Response.Write("<td>");
            Response.Write(TransferUtility.Html(TransferUtility.FormatFileSize(file.Length)));
            Response.Write("</td>");
            Response.Write("<td>");
            Response.Write(TransferUtility.Html(file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
            Response.Write("</td>");
            Response.Write("<td><a class=\"button secondary\" target=\"_blank\" rel=\"noopener noreferrer\" href=\"");
            Response.Write(TransferUtility.Html(openUrl));
            Response.Write("\">Open</a></td>");
            Response.Write("<td><a class=\"button secondary\" href=\"");
            Response.Write(TransferUtility.Html(downloadUrl));
            Response.Write("\">Download</a></td>");
            Response.Write("<td>");

            if (allowDelete)
            {
                Response.Write("<button class=\"button danger delete-button\" type=\"button\" data-group=\"");
                Response.Write(TransferUtility.Html(group));
                Response.Write("\" data-file=\"");
                Response.Write(TransferUtility.Html(fileName));
                Response.Write("\">Delete</button>");
            }
            else
            {
                Response.Write("<span class=\"muted\">Locked</span>");
            }

            Response.Write("</td>");
            Response.Write("</tr>");
        }
    }
</script>

<!doctype html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>File Transfer Tool</title>
    <style>
        :root {
            color-scheme: light;
            --bg: #f7f8fb;
            --surface: #ffffff;
            --surface-2: #f0f3f8;
            --text: #1d2737;
            --muted: #657186;
            --line: #d9dfeb;
            --primary: #1f6feb;
            --primary-strong: #1754b8;
            --danger: #c24132;
            --danger-strong: #9f2f24;
            --ok: #147a46;
            --warn: #a15c07;
        }

        * { box-sizing: border-box; }
        body {
            margin: 0;
            background: var(--bg);
            color: var(--text);
            font: 14px/1.45 "Segoe UI", Arial, sans-serif;
        }
        .shell {
            width: min(1180px, calc(100% - 32px));
            margin: 0 auto;
            padding: 28px 0 44px;
        }
        header {
            display: flex;
            justify-content: space-between;
            gap: 18px;
            align-items: flex-end;
            margin-bottom: 18px;
        }
        h1 {
            margin: 0;
            font-size: clamp(24px, 4vw, 34px);
            line-height: 1.1;
            letter-spacing: 0;
        }
        h2 {
            margin: 0 0 14px;
            font-size: 18px;
            letter-spacing: 0;
        }
        .status-line {
            color: var(--muted);
            display: flex;
            flex-wrap: wrap;
            gap: 8px 14px;
            justify-content: flex-end;
        }
        .panel {
            background: var(--surface);
            border: 1px solid var(--line);
            border-radius: 8px;
            padding: 18px;
            margin-bottom: 18px;
        }
        .upload-grid {
            display: grid;
            grid-template-columns: 1fr 240px;
            gap: 16px;
            align-items: end;
        }
        .field-row {
            display: flex;
            flex-wrap: wrap;
            gap: 10px;
            align-items: center;
        }
        label {
            color: var(--muted);
            font-weight: 600;
        }
        input[type="file"],
        input[type="password"],
        input[type="text"] {
            width: 100%;
            border: 1px solid var(--line);
            border-radius: 6px;
            background: #fff;
            color: var(--text);
            min-height: 38px;
            padding: 8px 10px;
        }
        .segments {
            display: inline-flex;
            flex-wrap: wrap;
            gap: 6px;
            padding: 4px;
            border: 1px solid var(--line);
            border-radius: 8px;
            background: var(--surface-2);
        }
        .segments label {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            min-height: 32px;
            padding: 6px 10px;
            border-radius: 6px;
            color: var(--text);
            cursor: pointer;
        }
        .segments input { margin: 0; }
        .checkbox-option {
            display: inline-flex;
            align-items: center;
            gap: 7px;
            min-height: 42px;
            padding: 0 4px;
            color: var(--text);
            cursor: pointer;
        }
        .checkbox-option input { margin: 0; }
        .button {
            display: inline-flex;
            justify-content: center;
            align-items: center;
            min-height: 36px;
            padding: 8px 12px;
            border: 1px solid transparent;
            border-radius: 6px;
            background: var(--primary);
            color: #fff;
            font-weight: 700;
            text-decoration: none;
            cursor: pointer;
            white-space: nowrap;
        }
        .button:hover { background: var(--primary-strong); }
        .button.secondary {
            background: #fff;
            color: var(--primary);
            border-color: #b7c9ef;
        }
        .button.secondary:hover { background: #eef4ff; }
        .button.danger {
            background: #fff;
            color: var(--danger);
            border-color: #efb9b3;
        }
        .button.danger:hover {
            background: #fff1ef;
            color: var(--danger-strong);
        }
        .button:disabled {
            opacity: .6;
            cursor: not-allowed;
        }
        .meta {
            margin-top: 8px;
            color: var(--muted);
            font-size: 13px;
        }
        .queue {
            display: grid;
            gap: 10px;
            margin-top: 14px;
            max-height: 420px;
            overflow-y: auto;
            overscroll-behavior: contain;
        }
        .queue-item {
            display: grid;
            grid-template-columns: minmax(0, 1fr) 140px;
            gap: 10px;
            align-items: center;
            padding: 10px;
            border: 1px solid var(--line);
            border-radius: 8px;
            background: #fbfcff;
        }
        .queue-name {
            min-width: 0;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
            font-weight: 700;
        }
        .queue-state {
            color: var(--muted);
            text-align: right;
            font-variant-numeric: tabular-nums;
        }
        .progress {
            grid-column: 1 / -1;
            width: 100%;
            height: 8px;
            overflow: hidden;
            border-radius: 999px;
            background: #e6ebf3;
        }
        .progress > span {
            display: block;
            width: 0;
            height: 100%;
            background: var(--primary);
            transition: width .18s ease;
        }
        .queue-item.done .progress > span { background: var(--ok); }
        .queue-item.error .progress > span { background: var(--danger); }
        .queue-item > .meta { grid-column: 1 / -1; }
        .tabs {
            display: flex;
            gap: 8px;
            margin: 2px 0 14px;
        }
        .tab {
            border: 1px solid var(--line);
            background: #fff;
            color: var(--text);
            border-radius: 6px;
            padding: 8px 12px;
            cursor: pointer;
            font-weight: 700;
        }
        .tab[aria-selected="true"] {
            background: var(--text);
            border-color: var(--text);
            color: #fff;
        }
        .table-wrap { overflow-x: auto; }
        table {
            width: 100%;
            border-collapse: collapse;
            min-width: 820px;
        }
        th,
        td {
            padding: 11px 10px;
            border-bottom: 1px solid var(--line);
            text-align: left;
            vertical-align: middle;
        }
        th {
            color: var(--muted);
            font-size: 12px;
            text-transform: uppercase;
            letter-spacing: 0;
        }
        td.name {
            max-width: 460px;
            font-weight: 700;
        }
        td.name span {
            display: block;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
        }
        .empty,
        .muted { color: var(--muted); }
        .auth-panel {
            max-width: 520px;
            margin: 80px auto;
        }
        .auth-panel form {
            display: grid;
            gap: 12px;
        }
        .hidden { display: none; }
        .notice {
            color: var(--warn);
            margin: 0 0 14px;
        }

        @media (max-width: 760px) {
            header {
                display: block;
            }
            .status-line {
                justify-content: flex-start;
                margin-top: 10px;
            }
            .upload-grid,
            .queue-item {
                grid-template-columns: 1fr;
            }
            .queue-state {
                text-align: left;
            }
            .button {
                width: 100%;
            }
        }
    </style>
</head>
<body>
<% if (!IsAuthorized && TokenEnabled) { %>
    <main class="shell">
        <section class="panel auth-panel">
            <h1>File Transfer Tool</h1>
            <p class="notice">Access token required.</p>
            <form id="unlockForm" method="get" action="default.aspx">
                <label for="token">Access token</label>
                <input id="token" name="token" type="password" autocomplete="current-password" required>
                <button class="button" type="submit">Unlock</button>
            </form>
        </section>
    </main>
<% } else { %>
    <main class="shell">
        <header>
            <div>
                <h1>File Transfer Tool</h1>
            </div>
            <div class="status-line">
                <span>Chunk: <%= TransferUtility.Html(TransferUtility.FormatFileSize(MaxChunkBytes)) %> (<%= TransferUtility.Html(MaxChunkBytesSource) %>)</span>
                <span>Parallel: <%= ParallelUploads.ToString(CultureInfo.InvariantCulture) %></span>
                <span>Limit: <%= MaxFileBytes > 0 ? TransferUtility.Html(TransferUtility.FormatFileSize(MaxFileBytes)) : "unlimited" %></span>
            </div>
        </header>

        <section class="panel">
            <h2>Upload</h2>
            <form id="uploadForm" method="post" enctype="multipart/form-data" action="upload.aspx">
                <div class="upload-grid">
                    <div>
                        <label for="fileInput">Files</label>
                        <input type="file" name="file" id="fileInput" multiple>
                    </div>
                    <button id="uploadButton" class="button" type="submit">Upload</button>
                </div>

                <div class="field-row" style="margin-top:12px">
                    <label>Destination</label>
                    <div class="segments" role="radiogroup" aria-label="Destination">
                        <label><input type="radio" name="uploadGroup" value="group1" checked>Deletable</label>
                        <label><input type="radio" name="uploadGroup" value="group2">Read-only</label>
                        <label><input type="radio" name="uploadGroup" value="group3">Hidden</label>
                    </div>
                    <label class="checkbox-option"><input id="keepDuplicates" name="keepDuplicates" type="checkbox" value="true">保留重复文件</label>
                </div>
                <div id="uploadStatus" class="meta" aria-live="polite">上传 0/0，上传失败 0</div>

                <% if (TokenEnabled) { %>
                <div style="margin-top:12px">
                    <label for="accessToken">Access token</label>
                    <input id="accessToken" name="accessToken" type="password" autocomplete="current-password" value="<%= TransferUtility.Html(RequestToken) %>">
                </div>
                <% } else { %>
                    <input id="accessToken" name="accessToken" type="hidden" value="">
                <% } %>
            </form>
            <div id="uploadMeta" class="meta"></div>
            <div id="queue" class="queue" aria-live="polite"></div>
        </section>

        <section class="panel">
            <div class="tabs" role="tablist" aria-label="File groups">
                <button class="tab" type="button" role="tab" aria-selected="true" data-panel="group1Panel">Deletable</button>
                <button class="tab" type="button" role="tab" aria-selected="false" data-panel="group2Panel">Read-only</button>
            </div>

            <div id="group1Panel" class="table-wrap" role="tabpanel">
                <table>
                    <thead>
                        <tr>
                            <th>File</th>
                            <th>Size</th>
                            <th>Modified</th>
                            <th>Open</th>
                            <th>Download</th>
                            <th>Delete</th>
                        </tr>
                    </thead>
                    <tbody>
                        <% RenderFileRows(TransferUtility.GroupDeletable, true); %>
                    </tbody>
                </table>
            </div>

            <div id="group2Panel" class="table-wrap hidden" role="tabpanel">
                <table>
                    <thead>
                        <tr>
                            <th>File</th>
                            <th>Size</th>
                            <th>Modified</th>
                            <th>Open</th>
                            <th>Download</th>
                            <th>Delete</th>
                        </tr>
                    </thead>
                    <tbody>
                        <% RenderFileRows(TransferUtility.GroupReadOnly, false); %>
                    </tbody>
                </table>
            </div>
        </section>
    </main>
<% } %>

<script>
(function () {
    var tokenEnabled = <%= TokenEnabled ? "true" : "false" %>;
    var initialToken = "<%= TransferUtility.JavaScript(RequestToken) %>";
    var maxChunkBytes = <%= MaxChunkBytes.ToString(CultureInfo.InvariantCulture) %>;
    var parallelUploads = <%= ParallelUploads.ToString(CultureInfo.InvariantCulture) %>;
    var maxFileBytes = <%= MaxFileBytes.ToString(CultureInfo.InvariantCulture) %>;

    if (tokenEnabled && !initialToken) {
        var savedToken = window.localStorage.getItem("transferAccessToken");
        if (savedToken) {
            window.location.href = "default.aspx?token=" + encodeURIComponent(savedToken);
            return;
        }
    }

    var unlockForm = document.getElementById("unlockForm");
    if (unlockForm) {
        unlockForm.addEventListener("submit", function () {
            var tokenField = document.getElementById("token");
            if (tokenField && tokenField.value) {
                window.localStorage.setItem("transferAccessToken", tokenField.value);
            }
        });
        return;
    }

    var uploadForm = document.getElementById("uploadForm");
    var fileInput = document.getElementById("fileInput");
    var uploadButton = document.getElementById("uploadButton");
    var uploadMeta = document.getElementById("uploadMeta");
    var uploadStatus = document.getElementById("uploadStatus");
    var queue = document.getElementById("queue");
    var accessToken = document.getElementById("accessToken");
    var keepDuplicates = document.getElementById("keepDuplicates");
    var uploadRunning = false;

    if (accessToken && initialToken) {
        accessToken.value = initialToken;
        window.localStorage.setItem("transferAccessToken", initialToken);
    }

    document.querySelectorAll(".tab").forEach(function (tab) {
        tab.addEventListener("click", function () {
            document.querySelectorAll(".tab").forEach(function (item) {
                item.setAttribute("aria-selected", item === tab ? "true" : "false");
            });
            document.querySelectorAll("[role='tabpanel']").forEach(function (panel) {
                panel.classList.toggle("hidden", panel.id !== tab.getAttribute("data-panel"));
            });
        });
    });

    document.querySelectorAll(".delete-button").forEach(function (button) {
        button.addEventListener("click", function () {
            var fileName = button.getAttribute("data-file");
            var group = button.getAttribute("data-group");
            if (!window.confirm("Delete " + fileName + "?")) {
                return;
            }

            button.disabled = true;
            var body = new URLSearchParams();
            body.set("file", fileName);
            body.set("group", group);
            body.set("accessToken", getToken());

            fetch("delete.aspx", {
                method: "POST",
                headers: buildHeaders({ "Content-Type": "application/x-www-form-urlencoded" }),
                body: body.toString()
            }).then(parseJsonResponse).then(function () {
                window.location.reload();
            }).catch(function (error) {
                button.disabled = false;
                window.alert(error.message);
            });
        });
    });

    if (fileInput) {
        fileInput.addEventListener("change", function () {
            var files = Array.prototype.slice.call(fileInput.files || []);
            var total = files.reduce(function (sum, file) { return sum + file.size; }, 0);
            uploadMeta.textContent = files.length ? files.length + " file(s), " + formatBytes(total) + " selected" : "";
        });
    }

    if (uploadForm) {
        uploadForm.addEventListener("submit", function (event) {
            if (!window.FormData || !window.fetch || !File.prototype.slice) {
                return;
            }

            event.preventDefault();
            if (uploadRunning) {
                return;
            }
            var files = Array.prototype.slice.call(fileInput.files || []);
            if (!files.length) {
                window.alert("Select at least one file.");
                return;
            }

            runUploadQueue(files);
        });
    }

    async function runUploadQueue(files) {
        uploadRunning = true;
        uploadButton.disabled = true;
        queue.innerHTML = "";
        var uploadedCount = 0;
        var failedCount = 0;
        var nextFile = 0;
        var group = getSelectedGroup();
        var preserveDuplicates = !!(keepDuplicates && keepDuplicates.checked);
        updateUploadStatus(uploadedCount, files.length, failedCount);

        async function fileWorker() {
            while (nextFile < files.length) {
                var index = nextFile++;
                try {
                    await uploadFile(files[index], index, group, preserveDuplicates);
                    uploadedCount++;
                } catch (error) {
                    failedCount++;
                }
                updateUploadStatus(uploadedCount, files.length, failedCount);
            }
        }

        try {
            var workers = [];
            var workerCount = Math.min(parallelUploads, files.length);
            for (var i = 0; i < workerCount; i++) {
                workers.push(fileWorker());
            }
            await Promise.all(workers);
        } finally {
            uploadRunning = false;
            uploadButton.disabled = false;
        }
    }

    async function uploadFile(file, index, group, preserveDuplicates) {
        var row = createQueueItem(file);
        if (maxFileBytes > 0 && file.size > maxFileBytes) {
            var sizeError = new Error(file.name + " exceeds the configured file limit.");
            setQueueError(row, sizeError.message);
            throw sizeError;
        }

        var uploadId = createUploadId(index);
        var totalChunks = Math.max(1, Math.ceil(file.size / maxChunkBytes));
        var completedChunks = 0;
        var uploadedBytes = 0;
        var startedAt = Date.now();
        var finalResult = null;

        try {
            for (var chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++) {
                var start = chunkIndex * maxChunkBytes;
                var end = Math.min(file.size, start + maxChunkBytes);
                var blob = file.slice(start, end);
                var result = await uploadChunkWithRetry(file, blob, uploadId, group, chunkIndex, totalChunks, preserveDuplicates);

                completedChunks++;
                uploadedBytes += blob.size;
                if (result.complete) {
                    finalResult = result;
                }

                updateProgress(row, uploadedBytes, file.size, completedChunks, totalChunks, startedAt, result.merging);
            }
        } catch (error) {
            setQueueError(row, error.message);
            throw error;
        }

        if (!finalResult) {
            setQueueError(row, "Upload reached the server but no completion response was returned.");
            throw new Error("Upload completion could not be confirmed for " + file.name + ".");
        }

        row.classList.add("done");
        row.querySelector(".queue-state").textContent = "Done";
        row.querySelector(".progress > span").style.width = "100%";
        var detail = document.createElement("div");
        detail.className = "meta";
        detail.textContent = (finalResult.fileName !== file.name ? "Stored as " + finalResult.fileName + " · " : "") + "SHA-256 " + finalResult.sha256;
        row.appendChild(detail);
    }

    async function uploadChunk(file, blob, uploadId, group, chunkIndex, totalChunks, preserveDuplicates) {
        var data = new FormData();
        data.append("uploadId", uploadId);
        data.append("group", group);
        data.append("fileName", file.name);
        data.append("chunkIndex", String(chunkIndex));
        data.append("totalChunks", String(totalChunks));
        data.append("totalSize", String(file.size));
        data.append("keepDuplicates", preserveDuplicates ? "true" : "false");
        data.append("lastModified", typeof file.lastModified === "number" ? String(file.lastModified) : "");
        data.append("accessToken", getToken());
        data.append("chunk", blob, file.name + ".part" + chunkIndex);

        return fetch("upload_chunk.aspx", {
            method: "POST",
            headers: buildHeaders(),
            body: data
        }).then(parseJsonResponse);
    }

    async function uploadChunkWithRetry(file, blob, uploadId, group, chunkIndex, totalChunks, preserveDuplicates) {
        var lastError = null;
        for (var attempt = 0; attempt < 4; attempt++) {
            try {
                return await uploadChunk(file, blob, uploadId, group, chunkIndex, totalChunks, preserveDuplicates);
            } catch (error) {
                lastError = error;
                if (attempt === 3 || !isRetryableUploadError(error)) {
                    break;
                }
                await delay(500 * Math.pow(2, attempt));
            }
        }

        throw lastError;
    }

    function isRetryableUploadError(error) {
        return !error.status || error.status === 408 || error.status === 429 || error.status >= 500;
    }

    function delay(ms) {
        return new Promise(function (resolve) {
            window.setTimeout(resolve, ms);
        });
    }

    function createQueueItem(file) {
        var item = document.createElement("div");
        item.className = "queue-item";
        item.innerHTML = "<div class=\"queue-name\"></div><div class=\"queue-state\">Queued</div><div class=\"progress\"><span></span></div>";
        item.querySelector(".queue-name").textContent = file.name + " (" + formatBytes(file.size) + ")";
        queue.insertBefore(item, queue.firstChild);
        queue.scrollTop = 0;
        return item;
    }

    function updateUploadStatus(uploadedCount, totalCount, failedCount) {
        uploadStatus.textContent = "上传 " + uploadedCount + "/" + totalCount + "，上传失败 " + failedCount;
    }

    function updateProgress(row, uploadedBytes, totalBytes, completedChunks, totalChunks, startedAt, merging) {
        var percent = totalBytes === 0 ? 100 : Math.min(100, uploadedBytes / totalBytes * 100);
        var elapsed = Math.max(1, (Date.now() - startedAt) / 1000);
        var speed = uploadedBytes / elapsed;
        row.querySelector(".progress > span").style.width = percent.toFixed(2) + "%";
        row.querySelector(".queue-state").textContent = merging ? "Merging" : Math.floor(percent) + "% · " + completedChunks + "/" + totalChunks + " · " + formatBytes(speed) + "/s";
    }

    function setQueueError(row, message) {
        row.classList.add("error");
        row.querySelector(".queue-state").textContent = "Error";
        var detail = document.createElement("div");
        detail.className = "meta";
        detail.textContent = message;
        row.appendChild(detail);
    }

    function parseJsonResponse(response) {
        return response.text().then(function (text) {
            var data = {};
            try {
                data = text ? JSON.parse(text) : {};
            } catch (error) {
                throw new Error(text || response.statusText);
            }

            if (!response.ok || data.ok === false) {
                var responseError = new Error(data.error || response.statusText);
                responseError.status = response.status;
                throw responseError;
            }

            return data;
        });
    }

    function buildHeaders(extra) {
        var headers = extra || {};
        var token = getToken();
        if (token) {
            headers["X-Transfer-Token"] = token;
        }
        return headers;
    }

    function getToken() {
        return accessToken ? accessToken.value : "";
    }

    function getSelectedGroup() {
        var selected = document.querySelector("input[name='uploadGroup']:checked");
        return selected ? selected.value : "group1";
    }

    function createUploadId(index) {
        var random = new Uint32Array(2);
        if (window.crypto && window.crypto.getRandomValues) {
            window.crypto.getRandomValues(random);
        } else {
            random[0] = Math.floor(Math.random() * 0xffffffff);
            random[1] = Math.floor(Math.random() * 0xffffffff);
        }

        return Date.now().toString(36) + "-" + index + "-" + random[0].toString(36) + random[1].toString(36);
    }

    function formatBytes(bytes) {
        var units = ["B", "KB", "MB", "GB", "TB", "PB"];
        var value = Number(bytes) || 0;
        var unit = 0;
        while (value >= 1024 && unit < units.length - 1) {
            value = value / 1024;
            unit++;
        }
        return unit === 0 ? value.toFixed(0) + " " + units[unit] : value.toFixed(value >= 100 ? 1 : 2) + " " + units[unit];
    }
})();
</script>
</body>
</html>
