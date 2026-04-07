# [WIP] ReW9x (Reddit for Windows 9x)

ReW9x is a lightweight Reddit client for old Windows systems, focused on keeping basic Reddit reading usable on a Windows 9x-era systems.

> [!CAUTION]
> This project already has a usable reader flow, but it is still rough software.
> Expect UI quirks, unfinished flows, and layout bugs that need more polishing.

## Disclaimer
> This is an unofficial third-party Reddit client.

> Reddit's API policies, rate limits, and platform rules may change over time.
> Use this client at your own risk. The author and contributors are not
> responsible for Reddit account issues, API access changes, or service-side
> breakage caused by policy changes outside this repository.

## Features

### Implemented

- Anonymous mode
- Saved-account mode with refresh-token reuse
- Feed navigation:
  - `Home`
  - `Popular`
  - `News`
  - `Explore`
- Topic drawer with subreddit list
- Subreddit navigation from search and topic list
- Mixed search popup:
  - subreddit/community matches
  - post search results
- Post reading
- Comment viewing
- Inline image preview for supported image posts
- Separate image viewer window for larger image viewing

### Partially Implemented / Rough Edges
- Reddit OAuth login via manual authorization-code flow
- Search UX still needs more polish
- Search popup behavior is functional but still evolving
- Topic drawer sizing/layout is still being tuned
- Some shell interactions may feel approximate compared to modern Reddit
- UI layout can still behave badly in edge cases or after unusual resize paths
- The current shell is usable, but not yet “finished”

### Planned / TODO
- Better search UX and result presentation
- More shell/layout polish
- Better subreddit/topic navigation (implementing keybindings)
- Ability to create a new posts from this Client

## Building (Linux only)
Build the client with:

```bash
bash build.sh
```

The build script compiles the executable to:

```text
build/ReW9x.exe
```

It also copies the required runtime files into `build/`.

## Running

Example Wine launch:

```bash
WINEPREFIX=... wine build/ReW9x.exe
```

## Project Layout
- `src/app/` - entry point and app config
- `src/api/` - Reddit API client logic
- `src/models/` - domain models
- `src/ui/` - WinForms UI
- `src/utils/` - parser/storage/shared helpers
- `external/openssl_wrp/` - Custom OpenSSL wrapper

## OAuth Setup

Before building, edit:

[`src/app/app_config.cs`](/home/nixxo/build/win98/src/reddit98/src/app/app_config.cs)

You need to set:

- `ClientId`
- `RedirectUri`

The intended Reddit application type is:

- `installed app`

The client uses a manual callback flow:

1. Open the authorization URL in a normal browser
2. Sign in and approve access
3. Copy the redirected callback URL
4. Paste that callback URL back into the client

The `redirect uri` on Reddit's side must match the value in
`src/app/app_config.cs`.

The account state is stored in:

```text
account.json
```

This file is local runtime state and is not intended to be versioned.

## Runtime Files

The final `build/` directory is expected to contain:

- `ReW9x.exe`
- `native_tls.dll`
- `libssl-3.dll`
- `libcrypto-3.dll`
- `providers/legacy.dll`
- `cacert.pem`

These are copied from:

```text
external/openssl_wrp/runtime/
```

## Notes
...

## Credits

- OpenSSL build/source used for this project:
  https://github.com/DiscordMessenger/openssl
- The native TLS wrapper/runtime is expected from:
  `external/openssl_wrp/runtime/`
