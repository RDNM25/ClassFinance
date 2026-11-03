# Product

<!-- impeccable:product-schema 1 -->

## Platform

Windows desktop (WPF)

## Users

- **Primary:** Class treasurer (Bendahara), confirmed by the user. Uses the app to manage class funds, student bills, and payments.
- **Other roles present in the implementation:** Homeroom teacher (Wali Kelas) and student (Siswa). Their exact responsibilities and access needs are not established here.

## Product Purpose

Class Finance is a desktop app for managing classroom finances: maintaining student records, creating and tracking bills, recording payments and other transactions, and viewing the class balance and transaction history.

## Positioning

No distinct market position or comparative claim has been established. Do not invent one.

## Operating Context

- Used as a Windows desktop application built with .NET 8 and WPF.
- The current interface uses Indonesian labels and formats money in Indonesian rupiah.
- The main workflows include reviewing the dashboard, managing students and bills, recording payments or cash transactions, and reviewing transaction history.

## Capabilities and Constraints

- The current implementation includes authentication, role-aware UI, dashboard, student and bill management, payment status and history, manual payment entry, cash withdrawal, and light/dark themes.
- The repository is a .NET 8 Windows WPF application. Impeccable Live's browser-based HTML/CSS preview does not directly run or modify its native XAML UI.
- Data retention, backup, synchronization, and deployment expectations have not been confirmed.

## Evidence on Hand

- Existing implementation: `ClassFinance/` (WPF views, resources, models, and services).
- Existing brand asset: `ClassFinance/Assets/logo.png`.
- No user-provided external proof, testimonials, or market claims are established.

## Product Principles

- Keep balances, bills, payments, and transaction history clear and traceable.
- Preserve role-aware access and actions already represented in the app.
- Use the existing Indonesian terminology and rupiah conventions consistently.
