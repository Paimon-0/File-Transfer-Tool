# File Transfer Tool

File Transfer Tool is a small ASP.NET Web Forms application for transferring files on a trusted network or a controlled IIS deployment. It provides browser-based uploads, downloads, basic file grouping, optional token protection, retry-tolerant chunk handling, and large-file support without requiring a database.

## Features

- Chunked browser uploads for files larger than the IIS per-request limit.
- Configurable chunk size, upload parallelism, storage path, temporary path, and maximum file size.
- Atomic finalization: chunks are merged in the temporary upload directory first, then moved into the visible storage directory.
- Automatic cleanup for abandoned temporary uploads.
- SHA-256 reporting after a successful chunked upload.
- Optional timestamp-based renaming for duplicate file names.
- Inline file opening from the browser list, with sandboxed active content.
- Multi-file upload progress with a bounded, newest-first activity list.
- Optional access token checks through `TransferAccessToken`.
- Basic hardening headers and path normalization to reduce accidental exposure and traversal risks.
- Modification timestamp preservation for modern browser uploads.
- Optional creation, modification, and access timestamp fields for custom upload clients.

## Requirements

- IIS on Windows.
- .NET Framework 4.8 recommended.
- An application pool identity with read and write access to the configured storage and temporary directories.
- Enough free disk space for both temporary chunks and finalized files during active uploads.

## Quick Start

1. Publish or copy the project into an IIS site or virtual directory.
2. Ensure the application pool runs with .NET CLR v4.0 and integrated pipeline mode.
3. Grant the app pool identity write permission to:
   - `~/App_Data/TransferFiles`
   - `~/App_Data/TransferTemp`
4. Open `default.aspx`.
5. Set `TransferAccessToken` in `web.config` before exposing the site outside a fully trusted LAN.

## Configuration

Settings are stored in `web.config` under `<appSettings>`.

```xml
<add key="TransferAccessToken" value="" />
<add key="TransferStorageRoot" value="~/App_Data/TransferFiles" />
<add key="TransferTempRoot" value="~/App_Data/TransferTemp" />
<add key="TransferMaxChunkBytes" value="268435456" />
<add key="TransferMaxFileBytes" value="0" />
<add key="TransferParallelUploads" value="4" />
<add key="TransferTempMaxAgeMinutes" value="60" />
<add key="TransferTempCleanupIntervalMinutes" value="15" />
```

`TransferMaxChunkBytes` controls the browser chunk size. The default value is `268435456` bytes, which is 256 MiB. If you change it, also keep the IIS request limits larger than the chunk size to allow multipart form overhead:

```xml
<httpRuntime maxRequestLength="307200" executionTimeout="43200" />
<requestLimits maxAllowedContentLength="314572800" />
```

`maxRequestLength` is measured in KiB. `maxAllowedContentLength` is measured in bytes.

`TransferMaxFileBytes` set to `0` means no application-level total file size cap. Practical limits still include disk capacity, filesystem limits, reverse proxy limits, and IIS timeout behavior.

`TransferParallelUploads` is clamped by the application to avoid overwhelming a small IIS deployment. Larger chunks reduce request overhead, while smaller chunks recover more cheaply after an interrupted transfer.

`TransferMaxChunkBytes` is a maximum chunk size, not a minimum file size. A smaller file is sent as one chunk containing only its actual bytes; an empty file is also sent as one zero-byte chunk. No padding or special configuration is required.

## File Groups

The UI stores files in three groups:

- `group1`: normal files, deletable from the UI.
- `group2`: read-only files, visible and downloadable but not deletable from the UI.
- `group3`: hidden group support for files not intended for normal list display.

The exact on-disk root is controlled by `TransferStorageRoot`.

## Upload Flow

Modern browsers use `upload_chunk.aspx`:

1. The browser splits each file into chunks.
2. Each chunk is uploaded with file metadata and an upload id.
3. The server writes chunks under `TransferTempRoot`.
4. When all chunks are present, the server merges them into a temporary merged file.
5. The merged file is moved into `TransferStorageRoot`.
6. Temporary upload data is removed.

When **保留重复文件** is unchecked, an upload whose name already exists is rejected and the reason remains visible in the upload activity list. When checked, the existing file is preserved and the new file is stored as `name_yyyyMMddHHmmss.ext`, using the server's local upload time. If that timestamped name is also occupied, the server advances to the next available timestamp while keeping the same format.

The legacy form fallback posts to `upload.aspx`. It is kept for compatibility, but the chunked path is the preferred path for large files and metadata preservation.

## Timestamp Preservation

For browser uploads, the application sends the standard `File.lastModified` value with every chunk and applies it to the finalized file as `LastWriteTimeUtc`. This preserves the file modification date shown by the server file list and by HTTP `Last-Modified` download responses.

Important browser limitation: standard HTML file inputs do not expose the original file creation time or last access time. The browser UI therefore cannot reliably upload those values. Custom clients may provide these optional fields to `upload_chunk.aspx`:

- `createdUtc` or `creationTimeUtc`
- `lastModifiedUtc` or `lastModified`
- `lastAccessedUtc` or `lastAccessTimeUtc`

Timestamp values may be ISO-8601 UTC strings or Unix milliseconds. All chunks for the same upload must send identical timestamp metadata.

## Security Notes

- Set a long random `TransferAccessToken` before using the tool beyond a trusted local network.
- Serve the application over HTTPS when credentials or sensitive files are involved.
- Keep storage under `App_Data` or another non-browsable directory.
- Do not grant write permissions broader than the configured storage and temporary directories.
- Put any public reverse proxy limits above `TransferMaxChunkBytes`, otherwise uploads can fail before the application receives the request.

This project is intentionally lightweight. It is not a full enterprise document management system, and it does not provide user accounts, audit logs, antivirus scanning, or end-to-end encryption.

## Troubleshooting

If the page reports that the request body is too large, lower `TransferMaxChunkBytes` or raise both IIS request limits.

If uploads are slow, test with a larger `TransferMaxChunkBytes`, increase `TransferParallelUploads` cautiously, and verify that antivirus or network storage is not scanning every temporary chunk synchronously.

If temporary files remain after interrupted transfers, check that the app pool identity can delete files under `TransferTempRoot` and that `TransferTempMaxAgeMinutes` plus `TransferTempCleanupIntervalMinutes` match the desired cleanup window.

If timestamps are not preserved, confirm that the browser is using the chunked upload path. The legacy fallback form cannot provide reliable per-file timestamp metadata.
