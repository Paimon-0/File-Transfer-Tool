# File Transfer Tool

File Transfer Tool is a lightweight ASP.NET Web Forms application for moving files across a trusted local network. It is designed for quick browser-based transfers on Windows/IIS without requiring a database, background service, or external package dependency.

The current implementation uses chunked uploads, controlled downloads, optional token authentication, and server-side storage under `App_Data` so large files can be transferred without exposing uploaded content as executable web files.

## Platform

- Windows Server, Windows 10, or Windows 11
- IIS 7.0 or later
- ASP.NET / .NET Framework 4.8
- A writable application directory for `App_Data/TransferFiles` and `App_Data/TransferTemp`

## Key Features

- Chunked uploads for files larger than 4 GB.
- Parallel chunk transfer to improve upload throughput on LAN connections.
- Per-chunk retry in the browser for transient network failures.
- SHA-256 checksum returned after the server finishes merging an upload.
- Controlled download endpoint with HTTP `Range` support for resumable downloads.
- Optional shared access token for upload, download, and delete operations.
- POST-only delete endpoint; only the `group1` bucket is deletable.
- File name sanitization, path traversal protection, and HTML/URL encoding.
- Uploaded files are stored under `App_Data` and are not directly executed or served by IIS.
- Automatic cleanup of stale temporary upload sessions.
- Three storage groups:
  - `group1`: listed and deletable.
  - `group2`: listed and read-only.
  - `group3`: upload-only hidden bucket.

## Why Files Larger Than 4 GB Work

IIS request filtering uses `maxAllowedContentLength`, which is a per-request limit and cannot be used as a real solution for very large single multipart requests. This project avoids that ceiling by splitting a file in the browser and sending many smaller requests to `upload_chunk.aspx`.

Default settings:

- Browser chunk size: `16 MB`
- IIS per-request cap: `128 MB`
- Parallel uploads per file: `4`
- Total file size cap: unlimited by default

The practical maximum file size is determined by available disk space, filesystem limits, browser behavior, and request timeout settings.

## Installation

1. Enable IIS and ASP.NET 4.8 on the target Windows machine.
2. Copy the project files into an IIS application directory.
3. Ensure the application pool identity has read/write permissions to the application directory or to the configured storage paths.
4. Open `web.config` and set a strong `TransferAccessToken` before using the tool outside a fully trusted LAN.
5. Browse to `default.aspx`.

## Configuration

Configuration is stored in `web.config`.

```xml
<appSettings>
    <add key="TransferAccessToken" value="" />
    <add key="TransferStorageRoot" value="~/App_Data/TransferFiles" />
    <add key="TransferTempRoot" value="~/App_Data/TransferTemp" />
    <add key="TransferMaxChunkBytes" value="16777216" />
    <add key="TransferMaxFileBytes" value="0" />
    <add key="TransferParallelUploads" value="4" />
</appSettings>
```

| Setting | Description |
| --- | --- |
| `TransferAccessToken` | Optional shared token. Empty means token checks are disabled. |
| `TransferStorageRoot` | Final file storage root. Relative values are resolved from the web application root. |
| `TransferTempRoot` | Temporary chunk storage root. |
| `TransferMaxChunkBytes` | Maximum bytes accepted per chunk. Values are clamped between 1 MB and 128 MB. |
| `TransferMaxFileBytes` | Maximum total file size. `0` means unlimited. |
| `TransferParallelUploads` | Browser-side parallel chunk workers per file. Values are clamped between 1 and 8. |

IIS request filtering remains intentionally below 4 GB:

```xml
<requestLimits maxAllowedContentLength="134217728" />
```

Do not raise this value to work around large uploads. Large files should use the chunked upload path.

## Security Notes

This tool is intended for trusted environments. If you expose it to a wider network, use all of the following:

- Configure a long random `TransferAccessToken`.
- Serve the site over HTTPS.
- Restrict access at the firewall or reverse proxy.
- Keep ASP.NET and IIS patched.
- Keep storage under `App_Data` or another non-executable directory.

Important behavior:

- Uploaded files are never linked directly from `Files/`.
- Downloads are served through `download.aspx`.
- Delete requests must use POST.
- Only `group1` files can be deleted from the UI/API.
- File names are sanitized before storage.
- Resolved paths are checked to remain inside the configured storage root.

## Endpoints

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `default.aspx` | `GET` | Web interface. |
| `upload_chunk.aspx` | `POST` | Chunked upload endpoint used by the browser UI. |
| `upload.aspx` | `POST` | Small-file compatibility upload endpoint for browsers without chunk support. |
| `download.aspx` | `GET`, `HEAD` | Controlled download endpoint with `Range` support. |
| `delete.aspx` | `POST` | Deletes files from `group1` only. |

When `TransferAccessToken` is configured, clients must send the token as one of:

- `X-Transfer-Token` request header
- `accessToken` form field
- `token` query string parameter

The web UI stores the token in browser local storage for convenience.

## Operational Notes

- Temporary chunks are written to `TransferTempRoot` and merged after all chunks arrive.
- Completed files are moved into `TransferStorageRoot/<group>`.
- If a file name already exists, the server appends a UTC timestamp and numeric suffix.
- Stale temporary upload directories older than 24 hours are cleaned up opportunistically during uploads.
- Interrupted uploads can leave temporary data until cleanup runs.
- Downloads support HTTP range requests, so download managers and browsers can resume partial downloads when supported.

## Troubleshooting

### Upload fails immediately with 413 or request filtering errors

The chunk size is larger than the IIS per-request limit. Keep `TransferMaxChunkBytes` below `maxAllowedContentLength`.

### Upload completes but the file does not appear

Check that the IIS application pool identity can write to `TransferStorageRoot` and `TransferTempRoot`.

### Large upload finishes slowly after reaching 100%

The server is merging chunk files into the final file and computing SHA-256. This is expected for very large files and depends on disk speed.

### Token works for upload but not download

Download links include the token from the current `default.aspx?token=...` request. Reopen the UI with the token or use the `X-Transfer-Token` header from a custom client.

## Limitations

- There is no user account system; authentication is a shared-token model.
- Upload resume after a full browser refresh is not exposed in the UI.
- Folder upload is not implemented.
- The hidden group is intentionally not listed in the UI.

## License

This project is released under the MIT License. See [LICENSE](LICENSE).
