# ZemenServe — Hotel & Restaurant F&B Management System

ZemenServe is a lightweight, offline-first, native Windows desktop point-of-sale (POS) and food & beverage (F&B) management system designed for hotels and restaurants in Ethiopia. It facilitates real-time communication between the Cashier station and the Kitchen display over a local wired LAN (Local Area Network), allowing instant order synchronization, ingredient-level inventory control, and daily PDF-based profit reporting.

---

## 🚀 Key Features

- **Real-Time Order Tracking**: Smooth order transmission from Cashier to Kitchen (under 1 second LAN latency) with state transitions: `Pending` ➔ `Preparing` ➔ `Ready` ➔ `Served`.
- **Ingredient-Level Inventory**: Automatic deduction of raw ingredients per recipe consumption in a single atomic database transaction.
- **Low-Stock Alerting**: High-visibility alerts when ingredient stock levels drop below configurable thresholds.
- **Decoupled Digital Menu**: Standalone menu lookup and configuration module that can be edited independently.
- **QuestPDF Daily Reports**: Generates professional PDF reports at the end of the day calculating Total Revenue, Cost of Goods Sold (COGS), Net Profit, and item-by-item sales breakdowns.
- **Zero Configuration & Single-File Bundles**: Fully self-contained single `.exe` file deployments containing their own runtime and database server wrapper.

---

## 🛠️ Technology Stack & Licensing

All technologies selected are free for commercial use under community or open-source licenses:

| Layer | Technology | License |
| :--- | :--- | :--- |
| **Language** | C# / .NET 8 | MIT |
| **UI Framework** | WPF (Windows Presentation Foundation) | MIT / Open-source |
| **Database** | SQLite + EF Core (Entity Framework Core) | Public Domain / MIT |
| **Real-time Sync** | ASP.NET Core SignalR (Kestrel self-hosted server) | Apache-2.0 |
| **Reporting** | QuestPDF | QuestPDF Community License (Free under $1M USD revenue) |

---

## 🏗️ Architectural Layout & Solution Structure

The project uses a standard 3-project Visual Studio solution structure:

```
ZemenServe.sln (Solution file)
 ├── ZemenServe.Shared/      (Class Library - data models, SignalR DTOs, interfaces)
 ├── ZemenServe.Cashier/     (WPF Desktop App - order input, local SQLite DB, self-hosted SignalR Hub, PDF generation)
 └── ZemenServe.Kitchen/     (WPF Desktop App - kitchen display queue, thin client SignalR connection)
```

- **`ZemenServe.Shared`**: A class library targeting `.net8.0`. It contains common records, constants, and events shared across both the Cashier and Kitchen applications.
- **`ZemenServe.Cashier`**: A WPF application targeting `.net8.0-windows`. On startup, it automatically hosts a Kestrel web server running a SignalR hub (`/orderhub`) and manages read/write queries to the local SQLite database.
- **`ZemenServe.Kitchen`**: A WPF application targeting `.net8.0-windows`. It functions as a thin real-time display client connecting to the Cashier PC's SignalR server over the local LAN.

---

## ⚙️ Prerequisites

To build and run ZemenServe locally, ensure you have:
1. **Windows OS** (WPF is a Windows-only desktop framework).
2. **.NET 8 SDK** (Download from [Microsoft .NET portal](https://dotnet.microsoft.com/download)).
3. **Visual Studio 2022** (with *.NET Desktop Development* workload enabled) or **Visual Studio Code / JetBrains Rider**.

---

## 🛠️ Build and Development

### 1. Build the Solution
Open your terminal in the root directory and run:
```bash
dotnet restore ZemenServe.sln
dotnet build ZemenServe.sln
```

### 2. Run Cashier App (Host)
The Cashier app must be started first as it hosts the SQLite database and the SignalR server:
```bash
dotnet run --project ZemenServe.Cashier
```
The Cashier app starts the embedded server automatically on `http://localhost:5000` (or the configured static local IP).

### 3. Run Kitchen App (Client)
Ensure that the Kitchen app's config file (usually `settings.json` in the executable folder) points to the Cashier host's IP address. To run the Kitchen client:
```bash
dotnet run --project ZemenServe.Kitchen
```

---

## 📦 Packaging & Deployment

To build a standalone, self-contained single-file executable for production deployment:

### Compile Cashier Host
```bash
dotnet publish ZemenServe.Cashier/ZemenServe.Cashier.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true
```

### Compile Kitchen Client
```bash
dotnet publish ZemenServe.Kitchen/ZemenServe.Kitchen.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The resulting executables will be generated in their respective `bin/Release/net8.0-windows/win-x64/publish/` folders and can be deployed directly via copy-paste. No separate .NET runtime installation is required on the target machines.
