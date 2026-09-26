# 01 — Project Overview

## Vision
FieldOps gives a field-service company one place to take a customer request, turn it into a work order, schedule the right technician, and let that technician do the job from a phone. It then records the parts and time used and produces an invoice, so the company always knows the status of every job.

## Problem it solves
- Jobs are currently tracked in WhatsApp, Excel, and phone calls, so jobs get lost and nobody knows their status.
- Dispatchers cannot see technician availability at a glance.
- Technicians have no single source of the job details, the site access notes, or the equipment history.
- Parts used on a job are not recorded, so stock drifts and gets under-billed.
- Invoicing is slow because nobody knows exactly what was done.

## Personas
| Persona | Device | Main goals |
|---|---|---|
| **Admin** (owner/manager) | Desktop | Manage users, settings, rates, and the parts catalog. See the dashboard and reports. Issue invoices. |
| **Dispatcher** (office) | Desktop | Log requests, create work orders, assign and schedule technicians, and follow up on status. |
| **Technician** (field) | Mobile (PWA) | See today's jobs, navigate to the site, update status, follow the checklist, add notes, photos, and parts, and capture the customer's signature. |
| *Customer* (later) | Mobile/Desktop | Request service and track job status. **Not in MVP.** |

## MVP scope (in)
1. Authentication, users, and roles (Admin, Dispatcher, Technician)
2. Customers, contacts, and sites (with map location)
3. Assets/equipment at sites, with service history
4. Technicians, skills, and time off
5. Work orders: create, edit, priority, type, checklist tasks, and notes, with a strict status workflow
6. Scheduling and a dispatch board (technicians × day timeline, drag-and-drop)
7. Technician mobile app: my jobs, status updates, checklist, notes, photos, parts used, time tracking, and customer signature
8. Parts and inventory: catalog, warehouse and van stock, transfers, and consumption on jobs
9. Invoicing: generate from a completed work order (labor + parts + VAT), PDF, and a mark-as-paid action
10. Notifications: in-app (SignalR) and email for key events
11. Dashboard and basic reports

## Out of scope for MVP (later)
- Preventive maintenance plans (recurring auto-generated work orders). This is designed for in `03-database.md` but not built.
- Customer portal and customer SMS/WhatsApp notifications
- Offline mode for technicians
- Route optimization and live GPS tracking
- Quotes/estimates, contracts/SLAs, and payments gateway
- Arabic UI / RTL (keep the UI strings easy to extract later)
- Multi-tenancy (SaaS)

## Key business rules (summary)
- Every work order belongs to one customer and one of that customer's sites. It can optionally reference one asset at that site.
- A work order has **one assigned technician** in the MVP. Multi-technician jobs come later.
- Status changes must follow the state machine in `07-flows.md`. Every change is written to the status history along with the user and the time.
- A technician cannot be scheduled over their approved time off or over another job that overlaps. The dispatcher gets a warning and can override overlaps but not time off.
- Consuming a part on a job deducts stock from the technician's van. Stock can never go negative.
- A work order can be invoiced only once, and only when it is `Completed`. Once issued, an invoice is read-only. Corrections are made by voiding it and re-issuing.
- VAT defaults to 5% (UAE) and is configurable in settings.

## Success criteria for MVP
- A dispatcher can go from a phone call to a scheduled work order in under 2 minutes.
- A technician can complete a job end-to-end on a phone without calling the office.
- Every completed job has its time, parts, photos, and signature recorded.
- An invoice PDF can be generated in one click from a completed job.
