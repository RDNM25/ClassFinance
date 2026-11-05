# Product

<!-- impeccable:product-schema 1 -->

## Platform

Windows desktop with a custom charcoal-and-red finance-console design language.

## Users

The desktop app is primarily for the class Bendahara, who records class cash, student payments, bills, and expenses. Wali kelas and student roles exist in the codebase, but are not the primary audience for this desktop redesign.

## Product Purpose

KasMate helps a Bendahara manage one class's shared cash and student bill balances in one desktop app. Success means quickly understanding available class cash, which students still owe, and what action to take next.

## Positioning

The app connects each class bill to individual student balances and the class transaction ledger, so bill progress and recorded cash activity can be followed together.

## Operating Context

The Bendahara uses the app on a Windows desktop to review the cash balance, maintain the student roster, create and edit bills, record payments and expenses, and inspect transaction history. Students pay the Bendahara upfront; the desktop app records the payment and does not collect payment proof. The app connects directly to a local MariaDB database managed through XAMPP.

## Capabilities and Constraints

- Class cash balance and transaction history.
- Student roster with NIS and per-student bill/payment summaries.
- Class bills with per-student due amount, amount paid, and payment status.
- Payment, expense, cash withdrawal, overpayment balance, and refund workflows.
- New and updated bills create notifications for affected students; a separate mobile client consumes them.
- Preserve the existing workflows and Indonesian UI wording.
- WPF desktop application with light and dark themes; retain database-backed behavior.

## Brand Commitments

- Keep the product name “KasMate” and the supplied KasMate logo.
- Keep user-facing app copy in Indonesian.

## Evidence on Hand

- Existing WPF screens and models in `ClassFinance/Views` and `ClassFinance/Models`.
- Existing logo at `ClassFinance/Assets/kasmate-logo.jpeg`.
- Existing finance records in the local `kasmate` MariaDB database.
- Dribbble references supplied by the user: dashboard search results and school/finance dashboard examples. They are layout references, not sources of product features or data.

## Product Principles

1. Put available cash and outstanding student balances where the Bendahara can scan them quickly.
2. Keep every displayed amount tied to the real class, student, bill, or transaction data.
3. Make common finance actions easy to find while preserving the existing rules and confirmation steps.
4. Use Indonesian terms consistently across screens and notifications.
