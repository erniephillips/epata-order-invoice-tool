# EPATA Invoice Tool

This is a small local Windows-friendly app that hosts your EPATA estimate/invoice HTML page, opens it in your browser, and saves records into a real SQLite database file.

## What it does

- Double-click the app after publishing.
- Starts a local server at `http://127.0.0.1:5057/index.html`.
- Opens the browser automatically.
- Saves estimates/invoices to `App_Data/epata_invoices.sqlite` when you press Ctrl+S, click Save Draft, or generate/preview PDFs.
- Loads the most recent saved record when you reopen the app.
- Keeps record history in the Saved Records tab.
- Lets you export/import the SQLite backup.

## Development run

```bash
dotnet restore
dotnet run
```

Open:

```text
http://127.0.0.1:5057/index.html
```

## Publish for Windows double-click use

From the project folder:

```bash
!!!!!!!!!!!!!!!!!!!!!!!!IMPORTANT!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
DO NOT DELETE THE APP_DATA FOLDER! It contains the invoice records and is needed for the app to run properly.
Under C:\Users\ernie\OneDrive\Documents\__EPATA 3D Print Business Folder\07_Websites and Listings\EPATA Invoice  - Estimate Generator\EPATA.InvoiceTool\bin\Release\net10.0\win-x64\publish

dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=false
```

Then go to:

```text
bin\Release\net10.0\win-x64\publish\
```

Double-click:

```text
EPATA.InvoiceTool.exe
```

Leave the console window open while using the app. Closing it stops the local server.

## Database location

During development:

```text
EPATA.InvoiceTool\App_Data\epata_invoices.sqlite
```

After publish:

```text
publish\App_Data\epata_invoices.sqlite
```

## Important

The app now initializes/repairs the SQLite schema at startup and before API calls, which prevents blank or stale database files from causing `no such table: Documents`.

The PDF generation still uses pdfMake from CDN in the HTML page. That means the browser needs internet access to load the PDF library unless you later download and host pdfMake locally in `wwwroot`.
