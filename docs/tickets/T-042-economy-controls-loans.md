# T-042 — Economy controls: prices, admission fee, finances panel + loans

- **Priority**: 🟡 Feature
- **Type**: Engine / UI
- **Status**: ☐ To do
- **Parent**: [T-038](T-038-park-management-ui.md). **Needs**: [T-040](T-040-build-mode-foundation.md).

## Context

`ParkFinances` tracks the balance read-only with derived prices/fee. This ticket gives the player
control: per-ride ticket price, the park admission fee, a proper finances panel, and the loan system.

## Reference (original)

`mCash`/`InitialCash`, `mAdmissionFee`/`InitialAdmissionFee` ("Admission fee set to %d"), and a full
loan model: `LoanInfo`, `LOANNAMES::TableID`, `mLoans[loan].amount_available` /`APR_in_percent`
/`monthly_repayment`/`loan_bought`, plus "Accepted for this loan", "Cannot afford to pay off loan",
"Congratulations! You've finished paying off loan %d!", "Bankrupted". So loans have an APR + monthly
repayment, can be bought/repaid, and bankruptcy is a real state. Initial values come from the level
config (`InitialCash`/`InitialAdmissionFee`).

## Work

1. **Ride price**: select a ride (T-040) → panel to set its ticket price; peeps pay that on boarding
   (already wired via `Ride.TicketPrice`, make it settable).
2. **Admission fee**: a park panel to set `EntryFee`; seed from the level config (`InitialAdmissionFee`).
3. **Finances panel**: balance + income/cost breakdown (extend the dev `ParkStatsPanel`) with a
   running history/graph.
4. **Loans**: model `Loan { amount, APR, monthlyRepayment, bought }`; take/repay loans, monthly
   interest debit, and a **bankruptcy** state when cash stays negative.

## Acceptance criteria

- Setting a ride's price changes ticket revenue; setting the admission fee changes gate income; a loan
  adds cash and debits monthly repayments; sustained negative cash triggers bankruptcy.

## Affected files

`source/OpenTPW/World/ParkFinances.cs`, `source/OpenTPW/World/Rides/Ride.cs`,
`source/OpenTPW/UI/Widgets/ParkStatsPanel.cs` (+ new finance/loan panels), `Level.cs`.
