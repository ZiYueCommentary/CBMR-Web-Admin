<div align="center">

# CBMR Web Admin

A website that manages a CBMR server without launching the game, powered by *[CB2 CSharp Binding](https://github.com/EasyT-T/ccb)*.

![License](https://img.shields.io/badge/license-Apache-2.0-blue.svg)
<!-- ![Version](https://img.shields.io/badge/version-v0.1.2-brightgreen.svg) -->
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-14.0-239120?logo=csharp&logoColor=white)
![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)

</div>

## Overview

CBMR Web Admin has two parts that talk to each other over a local named pipe (`CbmrWebAdmin`), so both must run on the same machine:

- **WebPortal**: an ASP.NET Core Blazor website for server administrators. It shows online players, chat, reports, bans, mutes and the join/leave history, and offers server controls (broadcast, kick, ban, mute, restart, fix elevator, clear items). Admin accounts and the audit log are stored in its own SQLite database.
- **ServerBinding**: a [CCB](https://github.com/EasyT-T/ccb) plugin loaded by the CBMR game server. It answers the portal's requests, applies bans and mutes in game, and provides the in-game `/report` command. Its data (bans, mutes, reports, session log) is stored in a SQLite database next to the game server.

Always deploy the portal and the binding from the same version; a newer portal may send requests an older binding does not understand.

## Installation

### Prerequisites

- A CBMR server with [CCB](https://github.com/EasyT-T/ccb/releases) installed (follow its *Installing CCB* steps).
- [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0): the SDK to build, plus the x86 runtime for CCB and the ASP.NET Core runtime for the portal.

### 1. Build

```shell
git clone https://github.com/ZiYueCommentary/CBMR-Web-Admin.git
cd CBMR-Web-Admin
dotnet publish ServerBinding/ServerBinding.csproj -c Release -r win-x86 --no-self-contained -o publish/binding
dotnet publish WebPortal/WebPortal.csproj -c Release -o publish/portal
```

### 2. Install the binding

1. Copy `publish/binding/CbmrWebAdmin.ServerBinding.dll` into `path/to/server/ccb/plugins`. Both languages (English and Simplified Chinese) are embedded in this dll.
2. Copy the other `.dll` files from `publish/binding` (`CbmrWebAdmin.Shared.dll`, MessagePack, Entity Framework Core, SQLite and `Microsoft.Extensions.*`, including the native `e_sqlite3.dll`) into `path/to/server/ccb/dependencies`. Skip `CCB.dll`, which CCB already provides.
3. Start the server. The plugin creates its config `webadmin.json` on first load; `DatabasePath` (default `Data/webportal.db`) sets where its database is stored.

### 3. Install the portal

1. Copy the full `publish/portal` folder (or extract the `WebPortal` artifact from the Build workflow for the same version) to a folder on the same machine as the game server. Do not deploy `bin/` output from `dotnet build`, which can reference static assets on the build machine.
2. Edit `appsettings.json` there and add the database and listening address (change the port as you like):

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "DataSource=Data/webadmin.db;Cache=Shared"
     },
     "Kestrel": {
       "Endpoints": {
         "Http": { "Url": "http://*:5000" }
       }
     },
     "PipeReconnectDelaySeconds": 5
   }
   ```

   Keep the existing `Logging` and `AllowedHosts` entries. Create the `Data` folder if it does not exist.
3. Run `CbmrWebAdmin.WebPortal.exe` (or `dotnet CbmrWebAdmin.WebPortal.dll`) from that folder.
4. On first start the portal creates the primary admin account `admin` and prints its random password once in the console log. Save it, sign in at `http://<host>:5000`, and add other accounts from the *Accounts* page.

The portal reconnects to the binding automatically; until the game server is running it shows a "No connection with server" banner.

### Updating

Stop the portal and the server, replace the files of both with published output from the same version, then start them again. Your `appsettings.json`, `webadmin.json` and both databases are kept.
