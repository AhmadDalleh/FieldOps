# 05 — API

Base path: `/api`. All endpoints require a JWT except `auth/login` and `auth/refresh`. Swagger is available at `/swagger` in Development.
**Roles:** A = Admin, D = Dispatcher, T = Technician (own jobs only), Office = A or D.

## Common contracts
```jsonc
// PagedResult<T>
{ "items": [], "page": 1, "pageSize": 20, "totalCount": 134 }
// Query params on list endpoints: ?page=1&pageSize=20&search=...&sort=name|-createdAt
// Errors: ProblemDetails (see 02-architecture.md) + "code"
```

## Auth & users
| Method | Route | Roles | Story |
|---|---|---|---|
| POST | /auth/login | public | AUTH-01 |
| POST | /auth/refresh | public (refresh token) | AUTH-02 |
| POST | /auth/logout | any | AUTH-02 |
| GET | /auth/me | any | – |
| POST | /auth/change-password | any | AUTH-04 |
| GET/POST | /users | A | AUTH-03 |
| GET/PUT | /users/{id} | A | AUTH-03 |
| POST | /users/{id}/deactivate · /activate | A | AUTH-03 |
| GET/PUT | /settings | GET Office, PUT A | SET-01 |
| POST | /settings/logo | A | SET-01 |

## Customers, sites, assets
| Method | Route | Roles | Story |
|---|---|---|---|
| GET/POST | /customers | Office | CUS-01/02 |
| GET/PUT | /customers/{id} | Office | CUS-03/04 |
| POST | /customers/{id}/deactivate | Office | CUS-04 |
| GET/POST | /customers/{id}/contacts | Office | CUS-05 |
| PUT/DELETE | /customers/{id}/contacts/{contactId} | Office | CUS-05 |
| GET/POST | /customers/{id}/sites | Office | SITE-01 |
| GET/PUT | /sites/{id} | Office | SITE-02 |
| POST | /sites/{id}/deactivate | Office | SITE-02 |
| GET/POST | /sites/{id}/assets | Office | AST-01 |
| GET/PUT | /assets/{id} | Office (T read if on own job) | AST-01 |
| GET | /assets/{id}/history | Office, T* | AST-02 |

## Technicians
| Method | Route | Roles | Story |
|---|---|---|---|
| GET | /technicians?date=YYYY-MM-DD | Office | TEC-04 |
| GET/PUT | /technicians/{id} | GET Office, PUT A | TEC-01 |
| GET/POST | /skills · PUT /skills/{id} | GET Office, write A | TEC-02 |
| GET/POST | /time-off (T creates own; Office sees all) | any | TEC-03 |
| POST | /time-off/{id}/approve · /reject | Office | TEC-03 |

## Work orders
| Method | Route | Roles | Story |
|---|---|---|---|
| GET | /work-orders?status=New,Scheduled&priority=&type=&technicianId=&customerId=&from=&to= | Office | WO-02 |
| POST | /work-orders | Office | WO-01 |
| GET | /work-orders/{id} | Office, T* | WO-03 |
| PUT | /work-orders/{id} | Office | WO-04 |
| GET | /work-orders/{id}/history | Office, T* | WO-09 |
| POST | /work-orders/{id}/tasks · PUT/DELETE /tasks/{taskId} · POST /tasks/reorder | Office | WO-05 |
| POST | /work-orders/{id}/tasks/{taskId}/toggle | Office, T* | WO-05 |
| GET/POST | /work-orders/{id}/notes · PUT /notes/{noteId} | Office, T* | WO-06 |
| **Status actions** | | | WO-07 |
| POST | /work-orders/{id}/schedule `{technicianId, start, end, allowOverlap}` | Office | DSP-01 |
| POST | /work-orders/{id}/unassign | Office | DSP-04 |
| POST | /work-orders/{id}/dispatch | Office | DSP-03 |
| POST | /work-orders/dispatch-day `{technicianId, date}` | Office | DSP-03 |
| POST | /work-orders/{id}/en-route `{lat?, lng?}` | T* | TAPP-03 |
| POST | /work-orders/{id}/start `{lat?, lng?}` | T* | TAPP-04 |
| POST | /work-orders/{id}/hold `{note}` | Office, T* | TAPP-05 |
| POST | /work-orders/{id}/resume | Office, T* | WO-07 |
| POST | /work-orders/{id}/complete `{completionNotes, signedByName, signatureAttachmentId, skippedTasksReason?}` | T* | TAPP-08 |
| POST | /work-orders/{id}/cancel `{reason}` | Office | WO-08 |
| **Field data** | | | |
| GET/POST | /work-orders/{id}/attachments (multipart) | Office, T* | TAPP-06 |
| GET | /attachments/{id} (file stream) | Office, T* | TAPP-06 |
| DELETE | /attachments/{id} | uploader | TAPP-06 |
| GET/POST | /work-orders/{id}/parts `{partId, quantity}` | Office, T* | TAPP-07 |
| DELETE | /work-orders/{id}/parts/{lineId} | Office, T* | TAPP-07 |
| GET | /work-orders/{id}/time-entries · PUT /time-entries/{id} | Office, T* | TAPP-09 |

## Dispatch & tech app
| Method | Route | Roles | Story |
|---|---|---|---|
| GET | /dispatch/board?date=YYYY-MM-DD → `{technicians[], jobs[], timeOff[], unassigned[]}` | Office | DSP-02 |
| GET | /dispatch/map?date= | Office | DSP-05 |
| GET | /me/jobs?day=today\|tomorrow | T | TAPP-01 |
| GET | /me/van-stock | T | TAPP-07 |

## Inventory
| Method | Route | Roles | Story |
|---|---|---|---|
| GET/POST | /parts · GET/PUT /parts/{id} | GET Office, write A | INV-01 |
| GET | /stock?locationId=&lowOnly= | Office | INV-02 |
| GET | /stock-locations | Office | INV-02 |
| POST | /stock/receive `{partId, locationId, quantity}` | Office | INV-03 |
| POST | /stock/transfer `{partId, fromLocationId, toLocationId, quantity}` | Office | INV-04 |
| POST | /stock/adjust `{partId, locationId, newQuantity, reason}` | A | INV-05 |
| GET | /stock/movements?partId=&type=&from=&to= | Office | INV-06 |

## Invoicing
| Method | Route | Roles | Story |
|---|---|---|---|
| POST | /work-orders/{id}/invoice | Office | BIL-01 |
| GET | /invoices?status=&customerId=&from=&to=&overdue= | Office | BIL-07 |
| GET | /invoices/{id} | Office | BIL-02 |
| POST/PUT/DELETE | /invoices/{id}/lines[/{lineId}] | Office | BIL-02 |
| POST | /invoices/{id}/issue | A | BIL-03 |
| GET | /invoices/{id}/pdf | Office | BIL-04 |
| POST | /invoices/{id}/mark-paid `{paidAt, reference}` | A | BIL-05 |
| POST | /invoices/{id}/void `{reason}` | A | BIL-06 |

## Notifications, dashboard, reports
| Method | Route | Roles | Story |
|---|---|---|---|
| GET | /notifications?unreadOnly= | any | NOT-01 |
| POST | /notifications/{id}/read · /notifications/read-all | any | NOT-01 |
| WS | /hubs/notifications | any | NOT-01 |
| GET | /dashboard/today | Office | DSH-01 |
| GET | /reports/technicians?from=&to=&format=json\|csv | A | RPT-01 |
| GET | /reports/revenue?from=&to=&groupBy=month\|customer&format= | A | RPT-02 |
| GET | /reports/parts-usage?from=&to=&format= | A | RPT-03 |

As built (Phase 10):
- Report dates are inclusive Dubai dates. Without them a report covers the month so far; `from` after `to`, or more than 366 days, returns 400. `format=csv` downloads UTF-8 CSV (with a BOM for Excel) including a Total row.
- Dashboard: "jobs by status" counts every open work order (New to On hold, zeros included); unassigned = New; overdue = open and past `due_by`; a technician is busy while En route or In progress, off during approved time off, otherwise free. The urgent list is Urgent New jobs, soonest due first (at most 10).
- Technicians report: jobs by completion time; average duration runs from the first start to completion (holds included); work hours are logged Work time.
- Revenue report: Issued and Paid invoices by issue date (drafts and voided excluded), with paid and outstanding columns.
- Parts usage: parts taken for jobs in the period (returned parts drop out). Price is what was charged; cost uses the part's current unit cost, because the cost at the time of use is not stored.

`T*` means a technician is allowed only when the work order is assigned to them. Otherwise the request returns 403.
