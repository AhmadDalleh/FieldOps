# 07 — Flows & Diagrams

## 1. Work order state machine
```mermaid
stateDiagram-v2
    [*] --> New : create
    New --> Scheduled : schedule (Office)
    Scheduled --> Scheduled : reschedule (Office)
    Scheduled --> New : unassign (Office)
    Scheduled --> Dispatched : dispatch (Office)
    Dispatched --> Dispatched : reschedule (Office)
    Dispatched --> New : unassign (Office)
    Dispatched --> EnRoute : on my way (T)
    Dispatched --> InProgress : start (T)
    EnRoute --> InProgress : start (T)
    InProgress --> OnHold : hold + note (T/Office)
    OnHold --> InProgress : resume (T/Office)
    InProgress --> Completed : complete + signature (T)
    Completed --> Invoiced : invoice issued (system)
    Invoiced --> Completed : invoice voided (system)

    New --> Cancelled : cancel + reason (Office)
    Scheduled --> Cancelled
    Dispatched --> Cancelled
    EnRoute --> Cancelled
    InProgress --> Cancelled
    OnHold --> Cancelled
    Completed --> [*]
    Invoiced --> [*]
    Cancelled --> [*]
```

### Transition table (the source for `WorkOrder` domain tests)
| From \ Action | schedule | unassign | dispatch | enRoute | start | hold | resume | complete | cancel | invoice | voidInvoice |
|---|---|---|---|---|---|---|---|---|---|---|---|
| New | ✅→Scheduled | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ |
| Scheduled | ✅ (stay) | ✅→New | ✅→Dispatched | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ |
| Dispatched | ✅ (stay) | ✅→New | ❌ | ✅→EnRoute | ✅→InProgress | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ |
| EnRoute | ❌ | ❌ | ❌ | ❌ | ✅→InProgress | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ |
| InProgress | ❌ | ❌ | ❌ | ❌ | ❌ | ✅→OnHold | ❌ | ✅→Completed | ✅ | ❌ | ❌ |
| OnHold | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅→InProgress | ❌ | ✅ | ❌ | ❌ |
| Completed | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅→Invoiced | ❌ |
| Invoiced | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅→Completed |
| Cancelled | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |

Implementation tip: keep one private dictionary in `WorkOrder` that maps `(Status, Action)` to the next status, and one `Transition(action, userId, now, note, lat, lng)` method. Every public method (`Start`, `Hold`, …) checks its preconditions, for example that a signature is present, and then calls `Transition`. The domain test is then one `[Theory]` over the whole table above.

Side effects per transition, handled in the entity or handler:
| Transition | Side effects |
|---|---|
| → Scheduled | set technician and times, notify T |
| → New (unassign) | clear technician and times, notify T |
| → EnRoute | start Travel time entry |
| → InProgress (start) | `started_at` (first time only), stop Travel, start Work |
| → OnHold | stop Work, note required, notify Office |
| OnHold → InProgress | start Work |
| → Completed | stop Work, `completed_at`, notes and signature required, notify Office |
| → Cancelled | reason required, parts must be returned first, stop open entries, notify T |

## 2. Invoice state machine
```mermaid
stateDiagram-v2
    [*] --> Draft : generate from Completed WO
    Draft --> Draft : edit lines
    Draft --> Issued : issue (number assigned, WO→Invoiced)
    Draft --> [*] : delete draft
    Issued --> Paid : mark paid
    Issued --> Void : void + reason (WO→Completed)
    Paid --> [*]
    Void --> [*]
```

## 3. Happy path: phone call to invoice
```mermaid
sequenceDiagram
    actor C as Customer
    actor D as Dispatcher
    participant API
    actor T as Technician
    actor A as Admin

    C->>D: Phone call "AC not cooling"
    D->>API: search customer / create customer + site
    D->>API: POST /work-orders (Repair, High)
    API-->>D: WO-000123 (New)
    D->>API: POST /work-orders/{id}/schedule (tech1, 10:00-12:00)
    API-->>T: notification "New job assigned"
    D->>API: POST /work-orders/{id}/dispatch
    T->>API: GET /me/jobs?day=today
    T->>API: POST /en-route (lat,lng)
    T->>API: POST /start
    T->>API: POST /attachments (before photo)
    T->>API: POST /tasks/{id}/toggle (checklist)
    T->>API: POST /parts {filter-01, 1}
    T->>API: POST /attachments (signature png)
    T->>API: POST /complete {notes, signedByName, signatureId}
    API-->>D: notification "WO-000123 completed"
    D->>API: POST /work-orders/{id}/invoice
    A->>API: POST /invoices/{id}/issue
    A->>API: GET /invoices/{id}/pdf
    A-->>C: sends invoice PDF
```

## 4. Schedule with conflict checks
```mermaid
sequenceDiagram
    participant UI as Dispatch board
    participant H as ScheduleWorkOrderHandler
    participant DB
    UI->>H: schedule(woId, techId, start, end, allowOverlap=false)
    H->>DB: load WO, technician (+skills), approved time off, overlapping jobs
    alt time off overlaps
        H-->>UI: 409 Technician.OnTimeOff
    else other jobs overlap and !allowOverlap
        H-->>UI: 409 Schedule.Overlap {conflicts[]}
        UI->>UI: show dialog "Schedule anyway?"
        UI->>H: schedule(..., allowOverlap=true)
    end
    H->>H: wo.Schedule(tech, start, end) (state machine)
    H->>DB: SaveChanges (xmin)
    H-->>UI: 200 {warnings: ["Missing skill: HVAC"]}
    H-)UI: SignalR WorkOrderChanged → board refresh (P9)
```

## 5. Consume part (stock safety)
```mermaid
sequenceDiagram
    participant T as Tech app
    participant H as AddWorkOrderPartHandler
    participant DB
    T->>H: add part {partId, qty}
    H->>DB: BEGIN
    H->>DB: load stock_level (van, part) with xmin
    alt qty > available
        H-->>T: 409 Stock.Insufficient
    else ok
        H->>DB: stock_level.quantity -= qty
        H->>DB: insert stock_movement (Consume, woId)
        H->>DB: insert work_order_part (unit_price snapshot)
        H->>DB: COMMIT (xmin conflict → 409, client retries)
        H-->>T: 201
    end
```

## 6. Auth token refresh
```mermaid
sequenceDiagram
    participant UI as Angular interceptor
    participant API
    UI->>API: GET /work-orders (expired access token)
    API-->>UI: 401
    UI->>API: POST /auth/refresh {refreshToken}
    alt refresh valid
        API-->>UI: new access + refresh (old one revoked)
        UI->>API: retry GET /work-orders
    else revoked / expired
        API-->>UI: 401
        UI->>UI: logout → /login
    end
```

## 7. Screen map
```mermaid
flowchart LR
    Login --> OfficeShell
    Login --> TechShell
    subgraph OfficeShell[Office - desktop]
        Dash[Dashboard] --- WOList[Work orders] --- Board[Dispatch board] --- Map[Map]
        Cust[Customers] --> CustDetail[Customer detail<br/>Info/Contacts/Sites/Assets/WOs/Invoices]
        WOList --> WODetail[WO detail]
        Techs[Technicians] --- TimeOff[Time off]
        Inv[Parts & stock] --- Bills[Invoices] --- Reports --- Settings[Settings/Users]
    end
    subgraph TechShell[Technician - mobile]
        MyJobs[My jobs] --> JobDetail[Job detail]
        JobDetail --> Checklist --> Photos --> Parts --> Complete[Complete + signature]
        MyTime[Time off request]
    end
```

## 8. Wireframes (low-fi)

**Dispatch board**
```
┌ Unassigned (4) ─────┐ ┌ ◀  Sun 04 Oct 2026  ▶ ─────────────────────────────────────────────┐
│ ● WO-131 URGENT     │ │           07  08  09  10  11  12  13  14  15  16  17  18  19        │
│   Al Nahda · AC     │ │ Ahmed K.  ░░░░[WO-120 ████]      [WO-124 ██████]                    │
│ ● WO-129 HIGH       │ │ Ravi S.        [WO-122 ███████████]   ▒▒▒▒ time off ▒▒▒▒▒▒▒▒        │
│   Muwaileh · Pump   │ │ Omar F.   [WO-118 ██] [WO-125 ███]          [WO-127 █████]          │
│ ○ WO-128 MEDIUM     │ └─────────────────────────────────────────────────────────────────────┘
└ drag onto a row ────┘   block color = priority · border = status · click = WO detail drawer
```

**Tech app: job detail (360px)**
```
┌──────────────────────────┐
│ ← WO-000123     HIGH     │
│ AC not cooling           │
│ Status: DISPATCHED       │
│ 10:00 – 12:00            │
├──────────────────────────┤
│ 👤 Sara M.  📞 Call       │
│ 📍 Villa 12, Al Nahda     │
│ Gate code 4411 [Navigate]│
│ 🔧 Carrier 2.5T · SN…     │
│ History (3) ›            │
├──────────────────────────┤
│ Checklist 1/4 ›          │
│ Photos (0) ›  Parts (0) ›│
│ Notes (2) ›              │
├──────────────────────────┤
│ [   ON MY WAY   ]        │  ← primary button changes with status
└──────────────────────────┘
```
