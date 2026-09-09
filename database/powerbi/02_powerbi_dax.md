# Power BI semantic model and DAX

Connect Power BI Desktop to the MySQL database and import these views:

- `vw_PowerBI_Tickets`
- `vw_PowerBI_Outages`
- `vw_PowerBI_Classification`
- `vw_PowerBI_TicketHistory`
- `vw_PowerBI_Agents`

Create a proper Date table and mark it as the Date table using `Date[Date]`.
Create the relationship `Date[Date]` -> `vw_PowerBI_Tickets[CreatedAt]` using the date portion in Power Query (or a dedicated CreatedDate column).

Recommended measures:

```DAX
Total Tickets = COUNTROWS(vw_PowerBI_Tickets)

Open Tickets =
CALCULATE(
    [Total Tickets],
    NOT(vw_PowerBI_Tickets[Status] IN {"Closed", "Resolved", "Cancelled"})
)

Resolved Tickets =
CALCULATE(
    [Total Tickets],
    vw_PowerBI_Tickets[Status] IN {"Resolved", "Closed"}
)

SLA Breaches =
CALCULATE(
    [Total Tickets],
    vw_PowerBI_Tickets[IsSlaBreached] = TRUE()
)

SLA Compliance % =
DIVIDE([Total Tickets] - [SLA Breaches], [Total Tickets], 0)

Classification Rate % =
DIVIDE(
    CALCULATE([Total Tickets], NOT ISBLANK(vw_PowerBI_Tickets[ClassifiedAt])),
    [Total Tickets],
    0
)

Assignment Rate % =
DIVIDE(
    CALCULATE([Total Tickets], NOT ISBLANK(vw_PowerBI_Tickets[AssignedAt])),
    [Total Tickets],
    0
)

Average Resolution Minutes =
AVERAGEX(
    FILTER(vw_PowerBI_Tickets, NOT ISBLANK(vw_PowerBI_Tickets[ResolutionMinutes])),
    vw_PowerBI_Tickets[ResolutionMinutes]
)

Average First Response Minutes =
AVERAGEX(
    FILTER(vw_PowerBI_Tickets, NOT ISBLANK(vw_PowerBI_Tickets[FirstResponseMinutes])),
    vw_PowerBI_Tickets[FirstResponseMinutes]
)

Average AI Confidence =
AVERAGEX(
    FILTER(vw_PowerBI_Tickets, NOT ISBLANK(vw_PowerBI_Tickets[AiConfidence])),
    vw_PowerBI_Tickets[AiConfidence]
)

Active Outages =
CALCULATE(
    COUNTROWS(vw_PowerBI_Outages),
    NOT(vw_PowerBI_Outages[Status] IN {"Resolved", "Closed"})
)

Average Outage Duration Minutes =
AVERAGEX(
    FILTER(vw_PowerBI_Outages, NOT ISBLANK(vw_PowerBI_Outages[OutageDurationMinutes])),
    vw_PowerBI_Outages[OutageDurationMinutes]
)
```

Recommended report pages:

1. Executive Overview — KPI cards, ticket trend, categories, SLA, active outages.
2. Ticket Analysis — category, priority, status, region, source and resolution time.
3. SLA Analysis — compliance, breaches, due-vs-resolved, resolution time.
4. Agent & Team Performance — workload, resolution rate, SLA breaches.
5. Mass Outages — outage count, severity, affected customers, duration, regions.
6. ML Classification — AI confidence, predicted categories, overrides, processing time.
7. Resolution Analysis — average/median resolution and first response by category/team.
