# 04 — User Stories

Format: **ID — As a *role*, I want … so that …**, followed by acceptance criteria (AC). Each AC should become at least one test.
Roles: **A** = Admin, **D** = Dispatcher, **T** = Technician. "Office" means A or D.

---

## EPIC 0 — Foundation & Auth

**US-AUTH-01 — Log in.** As any user, I want to log in with my email and password so that I can access the features for my role.
- AC1: Valid credentials return an access token (15 min) and a refresh token (7 days). The UI redirects Office users to `/office/dashboard` and Technicians to `/tech/my-jobs`.
- AC2: Invalid credentials return 401 with a generic message that does not reveal which field was wrong.
- AC3: An inactive user cannot log in.
- AC4: 5 failed attempts lock the account for 15 minutes (Identity lockout).

**US-AUTH-02 — Stay logged in.** As any user, I want my session to refresh silently so that I am not kicked out while I work.
- AC1: An expired access token plus a valid refresh token produces a new pair, and the old refresh token is revoked.
- AC2: Reusing a revoked refresh token returns 401 and revokes the whole token chain.
- AC3: Logout revokes the current refresh token.

**US-AUTH-03 — Manage users.** As an A, I want to create, edit, and deactivate users and set their role so that I control access.
- AC1: An A can create a user with full name, email, phone, role, and a temporary password.
- AC2: Creating a user with the Technician role also creates a `technicians` row and a Van stock location.
- AC3: Deactivating a user blocks login and revokes their refresh tokens. Their historical records remain.
- AC4: A D or T calling these endpoints gets 403.

**US-AUTH-04 — Change my password.** As any user, I want to change my password.
- AC1: Requires the current password. The new password must meet the Identity policy (8+ characters, a digit, an uppercase letter).

**US-SET-01 — Company settings.** As an A, I want to edit the company name, address, TRN, logo, VAT rate, labor rate, invoice due days, and currency so that invoices are correct.
- AC1: The VAT rate must be between 0 and 100. The labor rate must be at least 0.
- AC2: Changes apply only to invoices generated after the change.

---

## EPIC 1 — Customers & Sites

**US-CUS-01 — Create a customer.** As Office, I want to create a customer so that I can log requests for them.
- AC1: Name and phone are required. Email must be valid if given. The type is Individual or Business.
- AC2: The system assigns a code `C-00001` sequentially.
- AC3: If a customer with the same phone already exists, a duplicate warning is returned. The user can still save by confirming (the `force=true` flag).

**US-CUS-02 — Search and list customers.** As Office, I want to search customers by name, code, phone, or email with paging so that I can find them fast.
- AC1: The search is case-insensitive and supports partial matches.
- AC2: Inactive customers are hidden by default. The filter `includeInactive=true` shows them.

**US-CUS-03 — View a customer.** As Office, I want a customer page with tabs for Info, Contacts, Sites, Assets, Work Orders, and Invoices so that I can see the full history.

**US-CUS-04 — Edit or deactivate a customer.** As Office, I want to update customer data or deactivate a customer.
- AC1: A customer with open work orders (not Completed, Invoiced, or Cancelled) cannot be deactivated. This returns 409.

**US-CUS-05 — Manage contacts.** As Office, I want to add, edit, and remove contacts for a customer, with one marked as primary.
- AC1: Marking a contact as primary unmarks the previous primary.

**US-SITE-01 — Add a site.** As Office, I want to add a site (address and a map pin) to a customer so that technicians know where to go.
- AC1: Name, address line 1, and city are required. Latitude must be between -90 and 90 and longitude between -180 and 180 when given.
- AC2: The UI lets the user drop a pin on a Leaflet map to set the coordinates.
- AC3: Access notes (gate code, parking, contact on site) are optional free text.

**US-SITE-02 — Edit or deactivate a site.** Same rule as US-CUS-04 AC1, applied to the site.

---

## EPIC 2 — Assets

**US-AST-01 — Register an asset at a site.** As Office, I want to record equipment (type, make, model, serial number, install date, warranty) so that jobs can reference it.
- AC1: Name and type are required, and the site must belong to an active customer.
- AC2: The same serial number and manufacturer combination must not exist twice. A duplicate returns a validation error.

**US-AST-02 — Asset service history.** As Office or T, I want to see all past work orders for an asset so that I understand recurring problems.
- AC1: The list is ordered by newest first and shows the WO number, date, type, technician, status, and completion notes.
- AC2: A T can view the history only for assets on work orders assigned to them.

**US-AST-03 — Warranty flag.** As Office, when I create a work order on an asset under warranty, I want to see a "Under warranty until …" badge.

---

## EPIC 3 — Technicians

**US-TEC-01 — Technician profile.** As an A, I want to set a technician's employee code, phone, color, hourly cost, working hours, and skills.
- AC1: The employee code is unique. The color is a valid hex value.

**US-TEC-02 — Manage skills.** As an A, I want to create and rename skills.
- AC1: Skill names are unique and case-insensitive.

**US-TEC-03 — Time off.** As a T, I want to request time off. As Office, I want to approve or reject it so that the schedule respects availability.
- AC1: `ends_at` must be after `starts_at`.
- AC2: Only approved time off blocks scheduling.
- AC3: If an approved time off overlaps already scheduled jobs, the approval response lists those jobs so they can be rescheduled.

**US-TEC-04 — Technician list with availability.** As Office, I want to see technicians with their skills and whether they are available today (a count of today's jobs and any time off).

---

## EPIC 4 — Work Orders (core)

**US-WO-01 — Create a work order.** As Office, I want to create a work order for a customer site so that the job is tracked.
- AC1: Customer, site (belonging to that customer), title, type, and priority are required. The asset is optional and must belong to the selected site.
- AC2: The number `WO-000001` is sequential and generated in the same transaction.
- AC3: The initial status is `New`, and a history row is written (from null to New).
- AC4: If the type has an active checklist template, its items are copied into `work_order_tasks`.
- AC5: Due-by is optional. If priority is Urgent and due-by is empty, due-by defaults to now + 4h.

**US-WO-02 — List and filter work orders.** As Office, I want to filter by status (multiple), priority, type, technician, customer, and date range, and search by number or title.
- AC1: Default sort is priority (Urgent first), then due-by ascending.
- AC2: Overdue items (due-by < now and not Completed, Invoiced, or Cancelled) are flagged with `isOverdue=true`.

**US-WO-03 — Work order detail.** As Office, I want one page showing the customer, site with a map, asset, status timeline, tasks, notes, photos, parts, time, signature, and invoice link.

**US-WO-04 — Edit a work order.** As Office, I want to edit the title, description, priority, type, due-by, and asset while the job is not yet completed.
- AC1: Editing is allowed only in the New, Scheduled, Dispatched, or OnHold statuses. Otherwise it returns 409.
- AC2: Concurrent edits are handled with xmin. The second writer gets 409 with a "reload" message.

**US-WO-05 — Manage tasks (checklist).** As Office, I want to add, reorder, and remove tasks. As a T, I want to tick tasks as done.
- AC1: A T can only toggle `is_done` and cannot add or remove tasks. Toggling records `done_at` and `done_by`.
- AC2: Tasks cannot change after the job is Completed.

**US-WO-06 — Notes.** As Office or T, I want to add notes to a work order. Internal notes are hidden from the customer-facing PDF.
- AC1: Notes are append-only. The author can edit their note within 15 minutes of creating it.

**US-WO-07 — Status workflow.** As the system, I want to enforce the state machine in `07-flows.md` so that data stays consistent.
- AC1: Every allowed transition succeeds and writes a history row with the user, the time, and optional coordinates.
- AC2: Every disallowed transition returns 409 `WorkOrder.InvalidTransition`.
- AC3: Only Office can Cancel (a reason is required) and put a job On Hold. Both Office and T can resume from On Hold.

**US-WO-08 — Cancel.** As Office, I want to cancel a work order with a reason.
- AC1: Allowed from any status before Completed. Any consumed parts must be returned first, otherwise it returns 409.
- AC2: The assigned technician is notified.

**US-WO-09 — Status timeline.** As Office, I want to see the history of status changes with who made them and when.

---

## EPIC 5 — Scheduling & Dispatch

**US-DSP-01 — Assign and schedule.** As Office, I want to assign a technician and set a start and end time so that the job is planned.
- AC1: This moves the status from New to Scheduled. Rescheduling an already Scheduled or Dispatched job keeps its status.
- AC2: `scheduled_end` must be after `scheduled_start`, and the duration must be between 15 minutes and 12 hours.
- AC3: Overlapping the technician's approved time off is blocked with 409.
- AC4: Overlapping another job of the same technician returns a warning list. It saves only with `allowOverlap=true`.
- AC5: If the job requires a skill the technician lacks, a warning is shown but the save is not blocked.
- AC6: The technician is notified (in-app, plus email).

**US-DSP-02 — Dispatch board.** As Office, I want a day view with technicians as rows and hours (07:00–20:00) as columns, with jobs drawn as blocks colored by priority.
- AC1: A left panel lists unassigned `New` work orders sorted by priority and due-by.
- AC2: Dragging a job from the panel onto a technician row at a time calls assign/schedule with a default duration of 2h.
- AC3: Dragging an existing block to another row or time reschedules it. Resizing a block changes the end time.
- AC4: Time off appears as grey blocks.
- AC5: Changes made by other dispatchers appear live via SignalR without a page reload.
- AC6: Prev/next day buttons and a date picker are available.

**US-DSP-03 — Dispatch to technician.** As Office, I want to "dispatch" scheduled jobs (singly or all of a technician's jobs for a day) so that the technician knows the job is confirmed.
- AC1: This moves the status from Scheduled to Dispatched. The job becomes visible as confirmed in the technician app.

**US-DSP-04 — Unassign.** As Office, I want to unassign a job, which returns it to New and clears the technician and times.
- AC1: Allowed only from Scheduled or Dispatched.

**US-DSP-05 — Map view.** As Office, I want a map of today's jobs with pins colored by status so that I can see the geographic spread.

---

## EPIC 6 — Technician Mobile App

**US-TAPP-01 — My jobs.** As a T, I want to see my jobs for today (and tomorrow in a tab) ordered by scheduled start.
- AC1: Only jobs assigned to me that are Scheduled, Dispatched, EnRoute, InProgress, or OnHold are shown.
- AC2: Each card shows the time, customer, site area, priority, and status.
- AC3: The screen is usable on a 360px-wide viewport.

**US-TAPP-02 — Job detail.** As a T, I want the job's description, customer contact (tap to call), site address and access notes, a "Navigate" button (opens Google Maps with coordinates), asset info, and the asset's history.
- AC1: A T requesting a job not assigned to them gets 403.

**US-TAPP-03 — On my way.** As a T, I want to tap "On my way" so that the office knows I am travelling.
- AC1: Moves the status from Dispatched to EnRoute, starts a Travel time entry, and captures the browser location if permitted.

**US-TAPP-04 — Start job.** As a T, I want to tap "Start" when I arrive.
- AC1: Moves the status from EnRoute (or Dispatched) to InProgress, sets `started_at`, stops any open Travel entry, and starts a Work entry.

**US-TAPP-05 — Pause / hold.** As a T, I want to put the job on hold with a reason (for example, "waiting for part").
- AC1: Moves the status from InProgress to OnHold, stops the Work entry, requires a note, and notifies the office.

**US-TAPP-06 — Photos.** As a T, I want to take or upload photos (before and after) so that there is evidence of the work.
- AC1: Photos are compressed on the client (max 1600px), limited to 10 MB, and must be jpeg, png, or webp.
- AC2: Allowed while EnRoute, InProgress, or OnHold. The uploader can delete their own photo until the job is Completed.

**US-TAPP-07 — Record parts used.** As a T, I want to pick parts from my van stock and enter quantities.
- AC1: Only parts with stock > 0 in my van are listed.
- AC2: Saving creates a `Consume` stock movement and a `work_order_parts` row with a unit price snapshot, all in one transaction.
- AC3: A quantity greater than the available stock returns 409, and stock never goes negative.
- AC4: Removing a part line before completion creates a `Return` movement back to the van.

**US-TAPP-08 — Complete job.** As a T, I want to complete the job with notes and the customer's signature.
- AC1: Requires completion notes. All tasks must be done, or the T must give a "skipped tasks" reason.
- AC2: The signature is drawn on a canvas and uploaded as a PNG Attachment of kind Signature. The signer's name is required.
- AC3: Moves the status from InProgress to Completed, sets `completed_at`, stops the open Work entry, and notifies the office.

**US-TAPP-09 — Time log.** As a T, I want to see and correct my time entries for a job before completion.
- AC1: A T can edit start and end times for their own entries only. The end must be after the start, and entries must not overlap each other.

**US-TAPP-10 — Installable app.** As a T, I want to install the app on my home screen.
- AC1: A PWA manifest and service worker are present. The app shell loads fast. **Offline data is not supported in MVP**, so the app shows a clear "You are offline" banner.

---

## EPIC 7 — Parts & Inventory

**US-INV-01 — Parts catalog.** As an A, I want to manage parts (SKU, name, unit, cost, price, reorder level).
- AC1: SKU is unique. Price and cost are at least 0.

**US-INV-02 — Stock by location.** As Office, I want to see stock per part per location (warehouse and each van), with low-stock highlighting (total below the reorder level).

**US-INV-03 — Receive stock.** As Office, I want to receive stock into the warehouse.
- AC1: Creates a `Receive` movement and increases `stock_levels`.

**US-INV-04 — Transfer stock.** As Office, I want to transfer parts from the warehouse to a van (and back).
- AC1: Atomic: the source decreases and the destination increases in one transaction. Insufficient stock at the source returns 409.

**US-INV-05 — Adjust stock.** As an A, I want to correct stock after a count, with a reason.
- AC1: Creates an `Adjust` movement. The resulting quantity must be at least 0.

**US-INV-06 — Movement history.** As Office, I want to see all movements for a part with filters for date, type, and location.

---

## EPIC 8 — Invoicing

**US-BIL-01 — Generate an invoice.** As an A or D, I want to create a draft invoice from a Completed work order.
- AC1: Allowed only if the status is Completed and no non-void invoice exists for the job. Otherwise it returns 409.
- AC2: Labor line: total Work time (in hours, rounded up to 0.25) × the labor rate from settings.
- AC3: Part lines: one line per `work_order_parts` row using its snapshot unit price.
- AC4: VAT equals the subtotal × VAT rate, rounded half away from zero to 2 decimals. Total equals the subtotal plus VAT.

**US-BIL-02 — Edit a draft.** As an A or D, I want to add, edit, or remove lines on a Draft invoice (for example, a call-out fee or discount as a negative "Other" line).
- AC1: Totals are recalculated on every change. The total cannot be negative.

**US-BIL-03 — Issue an invoice.** As an A, I want to issue the invoice so that it gets a number and becomes final.
- AC1: Moves the invoice from Draft to Issued, assigns an `INV-000001` number, sets the issue date to today and the due date to today plus the configured due days, and moves the work order from Completed to Invoiced.
- AC2: An issued invoice is read-only.

**US-BIL-04 — Invoice PDF.** As Office, I want to download a PDF with the company logo, TRN, customer info, lines, VAT, and total, plus the job summary (non-internal notes and the signature image).

**US-BIL-05 — Mark as paid.** As an A, I want to mark an invoice paid with a date and reference.

**US-BIL-06 — Void.** As an A, I want to void an issued invoice with a reason.
- AC1: Moves the invoice from Issued to Void and moves the work order from Invoiced back to Completed, so a new invoice can be generated. Paid invoices cannot be voided.

**US-BIL-07 — Invoice list.** As Office, I want to filter invoices by status, customer, and date, and see overdue invoices (Issued and past the due date).

---

## EPIC 9 — Notifications

**US-NOT-01 — In-app notifications.** As any user, I want a bell icon with an unread count and a list, where each notification links to the related item.
- AC1: Delivered live through SignalR. The user can mark one or all as read.

**US-NOT-02 — Events.**
| Event | Recipient | Channel |
|---|---|---|
| Job assigned or rescheduled to me | T | in-app + email |
| Job cancelled or unassigned from me | T | in-app + email |
| Job put On Hold | Office | in-app |
| Job completed | Office | in-app |
| Time off requested | Office | in-app |
| Time off approved or rejected | T | in-app |
| Part below reorder level after consumption | A | in-app |
- AC1: A failure to send email must never fail the business operation. It is logged and the operation continues.

---

## EPIC 10 — Dashboard & Reports

**US-DSH-01 — Office dashboard.** As Office, I want today's KPIs: jobs by status, unassigned count, overdue count, completed today, and technicians busy or free, plus a list of urgent unassigned jobs.

**US-RPT-01 — Jobs report.** As an A, I want completed jobs per technician for a date range (count, average duration, total work hours), exportable to CSV.

**US-RPT-02 — Revenue report.** As an A, I want revenue for issued and paid invoices by month and by customer for a date range, exportable to CSV.

**US-RPT-03 — Parts usage report.** As an A, I want the parts consumed in a date range (quantity, cost, price, margin).

---

## LATER (not in MVP; kept for context)
- **US-PM-01** Preventive maintenance plans that auto-create work orders X days before `next_due_on`.
- **US-PORTAL-01** Customer portal to request service and track status.
- **US-OFF-01** Offline mode for technicians.
- **US-MULTI-01** Multiple technicians per work order.
- **US-QUO-01** Quotes that can be converted to work orders.
- **US-I18N-01** Arabic UI (RTL).
