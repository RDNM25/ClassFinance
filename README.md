# Class Finance — WPF Desktop App

A desktop (Windows/WPF) rebuild of the "Class Finance" concept from your LKPD, adapted for a PC screen: a persistent left sidebar for navigation instead of a single phone-width column, plus a wider dashboard with summary cards.

## How to open it in Visual Studio

1. Unzip the folder anywhere on your machine.
2. Open **ClassFinance.sln** in Visual Studio 2022 (Community edition is fine).
   - Make sure the **.NET desktop development** workload is installed (Visual Studio Installer → Modify → check ".NET desktop development").
3. Press **F5** (or Ctrl+F5) to build and run.
   - Target framework is `net8.0-windows`. If you only have .NET 6/7 installed, either install the .NET 8 SDK, or edit `ClassFinance.csproj` and change `net8.0-windows` to the SDK you have (e.g. `net6.0-windows`).

## Demo accounts (seeded in memory, no real database yet)

| Role       | Username | Password  |
|------------|----------|-----------|
| Bendahara  | admin    | admin     |
| Wali Kelas | wali     | wali123   |
| Siswa      | keijan   | siswa123  |

The seed data (students `keijan`, `arunks`, `Dnenuy`, and the `beli galon` transaction) mirrors the screenshots you shared, so you can compare the two side by side.

## What's inside (matches the OOP / MVC section of your LKPD)

- **Models/** — `User` (abstract base) → `Bendahara`, `Siswa`, `WaliKelas` (inheritance + polymorphism via `GetDashboardTitle()`), plus `Kelas`, `Tagihan`, `TagihanSiswa`, `Transaksi`, `Notifikasi`. Each class carries the methods described in your document (`TambahPemasukan`, `TambahPengeluaran`, `BuatTagihan`, `CatatPembayaran`, `GenerateLaporan`, `LihatSaldoKas`, etc.) — this is the **Model** layer.
- **Services/DataStore.cs** — in-memory repository (swap for EF Core/SQLite later without touching any View).
- **Views/** — the **View** layer: `LoginPage`, `DashboardPage`, `RiwayatTransaksiPage`, `SiswaPage`, `TagihanPage`, and dialogs for adding students/transactions/tagihan/tarik kas.
- Code-behind event handlers act as the light **Controller** layer connecting Views to Models.

## Feature differences from the LKPD draft

- Monetization (subscriptions / ads) intentionally left out, per your request.
- Added a simple `Tagihan` (billing) workflow with per-student payment status, since it was already referenced in your Model section but not yet wired into a screen.
- Role-based visibility: only **Bendahara** can withdraw cash, record transactions, or create tagihan; **Wali Kelas** and **Siswa** get read-only views plus roster management for the homeroom teacher.
- Feel free to rename, add, or delete any class/method — the structure is intentionally simple so your group can extend it (e.g. swap the in-memory `DataStore` for a real database, add file-based proof-of-payment uploads, etc.) for the OOP/MVC rubric.
