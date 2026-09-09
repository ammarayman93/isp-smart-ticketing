-- ISP Smart Ticketing: large synthetic demo dataset for MySQL 8+
-- Intended for a development/demo database AFTER the normal EF migrations + DataSeeder.
-- Default volume: 200,000 tickets, 20,000 outages, 200,000 classification logs,
-- and ~600,000 ticket-history records.
--
-- Run:
--   USE isp_ticketing;
--   SOURCE generate_demo_data.sql;
--
-- The data is synthetic and contains no real customer information.

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- Rebuild only the synthetic/demo tables. This keeps Roles, Categories, Teams, Users,
-- and KnowledgeArticles created by the application seeder.
TRUNCATE TABLE ClassificationLogs;
TRUNCATE TABLE TicketHistories;
TRUNCATE TABLE Tickets;
TRUNCATE TABLE OutageEvents;

SET FOREIGN_KEY_CHECKS = 1;

DROP TEMPORARY TABLE IF EXISTS demo_numbers;
CREATE TEMPORARY TABLE demo_numbers (
    n INT NOT NULL PRIMARY KEY
);

-- 0..199999
INSERT INTO demo_numbers (n)
SELECT a.n + b.n * 10 + c.n * 100 + d.n * 1000 + e.n * 10000 + f.n * 100000
FROM
    (SELECT 0 n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
     UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) a
CROSS JOIN
    (SELECT 0 n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
     UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) b
CROSS JOIN
    (SELECT 0 n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
     UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) c
CROSS JOIN
    (SELECT 0 n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
     UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) d
CROSS JOIN
    (SELECT 0 n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
     UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) e
CROSS JOIN
    (SELECT 0 n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
     UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) f
ORDER BY 1;

-- 20,000 realistic outage events spread across the last ~2 years.
INSERT INTO OutageEvents
(Title, Description, Region, AffectedCategoryId, Severity, Status, DetectedAt,
 ResolvedAt, EstimatedAffectedCustomers, RootCause, CreatedBySystem, CreatedAt, UpdatedAt)
SELECT
    CONCAT('Network incident #', LPAD(n + 1, 6, '0')),
    CASE MOD(n, 6)
      WHEN 0 THEN 'Fiber link degradation affecting multiple subscribers.'
      WHEN 1 THEN 'Access node interruption detected by monitoring.'
      WHEN 2 THEN 'Upstream routing instability in the regional network.'
      WHEN 3 THEN 'Power interruption at an access site.'
      WHEN 4 THEN 'High packet loss and interface errors detected.'
      ELSE 'Planned maintenance caused temporary service impact.'
    END,
    ELT(1 + MOD(n, 10),
      'North','South','East','West','Central','Downtown','Suburbs','Industrial','University','Coastal'),
    (SELECT Id FROM Categories WHERE Name='Service Outage' LIMIT 1),
    MOD(n, 4),
    CASE
      WHEN MOD(n, 20) < 14 THEN 2
      WHEN MOD(n, 20) < 18 THEN 3
      WHEN MOD(n, 20) = 18 THEN 1
      ELSE 0
    END,
    DATE_SUB(UTC_TIMESTAMP(), INTERVAL (MOD(n, 730) + 1) DAY)
      - INTERVAL MOD(n, 23) HOUR,
    CASE
      WHEN MOD(n, 20) < 18
      THEN DATE_SUB(UTC_TIMESTAMP(), INTERVAL (MOD(n, 730) + 1) DAY)
           - INTERVAL MOD(n, 23) HOUR
           + INTERVAL (15 + MOD(n, 420)) MINUTE
      ELSE NULL
    END,
    5 + MOD(n * 37, 2500),
    ELT(1 + MOD(n, 6),
      'Fiber cut','Power failure','Upstream congestion','Hardware fault',
      'Configuration change','Unknown / under investigation'),
    1,
    DATE_SUB(UTC_TIMESTAMP(), INTERVAL (MOD(n, 730) + 1) DAY),
    UTC_TIMESTAMP()
FROM demo_numbers
WHERE n < 20000;

-- 200,000 tickets.
-- Category distribution is intentionally uneven so Power BI charts are meaningful.
INSERT INTO Tickets
(TicketNumber, CustomerId, CustomerName, CustomerPhone, CustomerRegion,
 Title, Description, CategoryId, Priority, Status, Source, AssignedToId, TeamId,
 OutageEventId, CreatedById, CreatedAt, ClassifiedAt, AssignedAt, FirstResponseAt,
 ResolvedAt, ClosedAt, SlaDueAt, IsSlaBreached, SentimentScore, AiConfidence, UpdatedAt)
SELECT
    CONCAT('TCK-', DATE_FORMAT(created_at, '%Y%m'), '-', LPAD(n + 1, 7, '0')),
    CONCAT('CUST-', LPAD(100000 + n, 8, '0')),
    ELT(1 + MOD(n, 20),
      'Ahmed Hassan','Sara Ali','Omar Khaled','Lina Saleh','Yousef Nasser',
      'Maya Ibrahim','Karim Haddad','Nour Sami','Rami Farah','Hala Mansour',
      'Adam George','Mariam Adel','Tarek Younis','Dina Samir','Fadi Saad',
      'Leen Ahmad','Samir Kanaan','Reem Naji','Walid Omar','Dana Fares'),
    CONCAT('+1-416-', LPAD(1000000 + MOD(n * 7919, 9000000), 7, '0')),
    ELT(1 + MOD(n, 10),
      'North','South','East','West','Central','Downtown','Suburbs','Industrial','University','Coastal'),
    ELT(1 + MOD(n, 10),
      'Internet service outage','Slow internet speed','Router connectivity problem',
      'Wi-Fi instability','Billing inquiry','General technical support',
      'Connection drops repeatedly','High latency complaint','Package and renewal issue','No connectivity'),
    CONCAT(
      'Synthetic support case ', n + 1,
      '. Customer reports ',
      CASE MOD(n, 10)
        WHEN 0 THEN 'a complete internet outage affecting the connection.'
        WHEN 1 THEN 'very slow download and browsing speeds.'
        WHEN 2 THEN 'a router that restarts or loses WAN connectivity.'
        WHEN 3 THEN 'weak Wi-Fi coverage and frequent wireless drops.'
        WHEN 4 THEN 'a billing or payment discrepancy.'
        WHEN 5 THEN 'a general technical issue requiring investigation.'
        WHEN 6 THEN 'intermittent connection drops during the day.'
        WHEN 7 THEN 'high latency and poor network responsiveness.'
        WHEN 8 THEN 'a subscription renewal or package question.'
        ELSE 'no internet access after a recent configuration change.'
      END,
      ' Case generated for analytics and load testing.'
    ),
    CASE
      WHEN MOD(n, 100) < 18 THEN (SELECT Id FROM Categories WHERE Name='Service Outage' LIMIT 1)
      WHEN MOD(n, 100) < 38 THEN (SELECT Id FROM Categories WHERE Name='Slow Speed' LIMIT 1)
      WHEN MOD(n, 100) < 54 THEN (SELECT Id FROM Categories WHERE Name='Router Issues' LIMIT 1)
      WHEN MOD(n, 100) < 70 THEN (SELECT Id FROM Categories WHERE Name='WiFi Problems' LIMIT 1)
      WHEN MOD(n, 100) < 88 THEN (SELECT Id FROM Categories WHERE Name='Billing' LIMIT 1)
      ELSE (SELECT Id FROM Categories WHERE Name='Other' LIMIT 1)
    END,
    CASE
      WHEN MOD(n, 100) < 18 THEN 3
      WHEN MOD(n, 100) < 38 THEN 2
      WHEN MOD(n, 100) < 70 THEN 1
      WHEN MOD(n, 100) < 88 THEN 0
      ELSE 1
    END,
    CASE
      WHEN MOD(n, 100) < 7 THEN 7
      WHEN MOD(n, 100) < 12 THEN 0
      WHEN MOD(n, 100) < 22 THEN 1
      WHEN MOD(n, 100) < 37 THEN 2
      WHEN MOD(n, 100) < 55 THEN 3
      WHEN MOD(n, 100) < 70 THEN 4
      WHEN MOD(n, 100) < 88 THEN 5
      ELSE 6
    END,
    MOD(n, 5),
    CASE
      WHEN MOD(n, 100) < 18 THEN (SELECT Id FROM Users WHERE Email='network@isp.com' LIMIT 1)
      WHEN MOD(n, 100) < 88 THEN
        ELT(1 + MOD(n, 3),
          (SELECT Id FROM Users WHERE Email='agent@isp.com' LIMIT 1),
          (SELECT Id FROM Users WHERE Email='network@isp.com' LIMIT 1),
          (SELECT Id FROM Users WHERE Email='billing@isp.com' LIMIT 1))
      ELSE NULL
    END,
    CASE
      WHEN MOD(n, 100) < 18 THEN (SELECT Id FROM Teams WHERE Name='Network Operations' LIMIT 1)
      WHEN MOD(n, 100) < 70 THEN (SELECT Id FROM Teams WHERE Name='Technical Support' LIMIT 1)
      WHEN MOD(n, 100) < 88 THEN (SELECT Id FROM Teams WHERE Name='Billing' LIMIT 1)
      ELSE NULL
    END,
    CASE
      WHEN MOD(n, 100) < 18 AND MOD(n, 5) <> 0
      THEN 1 + MOD(n, 19999)
      ELSE NULL
    END,
    (SELECT Id FROM Users WHERE Email='admin@isp.com' LIMIT 1),
    created_at,
    CASE WHEN MOD(n, 100) < 94 THEN created_at + INTERVAL (2 + MOD(n, 90)) MINUTE ELSE NULL END,
    CASE WHEN MOD(n, 100) < 88 THEN created_at + INTERVAL (5 + MOD(n, 180)) MINUTE ELSE NULL END,
    CASE WHEN MOD(n, 100) < 91 THEN created_at + INTERVAL (10 + MOD(n, 240)) MINUTE ELSE NULL END,
    CASE WHEN MOD(n, 100) < 88 THEN created_at + INTERVAL (60 + MOD(n, 10000)) MINUTE ELSE NULL END,
    CASE WHEN MOD(n, 100) < 78 THEN created_at + INTERVAL (120 + MOD(n, 12000)) MINUTE ELSE NULL END,
    created_at + INTERVAL
      CASE
        WHEN MOD(n, 100) < 18 THEN 4
        WHEN MOD(n, 100) < 38 THEN 8
        WHEN MOD(n, 100) < 70 THEN 12
        ELSE 24
      END HOUR,
    CASE
      WHEN MOD(n, 100) < 88
      THEN MOD(n * 17, 100) < CASE WHEN MOD(n, 100) < 18 THEN 12 ELSE 9 END
      ELSE 0
    END,
    ROUND(-1 + (MOD(n * 7919, 2001) / 1000), 3),
    ROUND(0.55 + (MOD(n * 3571, 450) / 1000), 3),
    UTC_TIMESTAMP()
FROM (
    SELECT n,
           DATE_SUB(UTC_TIMESTAMP(), INTERVAL (MOD(n * 13, 730) + 1) DAY)
             - INTERVAL MOD(n * 19, 24) HOUR
             - INTERVAL MOD(n * 37, 60) MINUTE AS created_at
    FROM demo_numbers
    WHERE n < 200000
) x;

-- One ML classification record per ticket.
INSERT INTO ClassificationLogs
(TicketId, PredictedCategoryId, PredictedPriority, ConfidenceScore,
 ModelVersion, ProcessingTimeMs, WasOverridden, CreatedAt)
SELECT
    t.Id,
    CASE
      WHEN MOD(t.Id, 100) < 19 THEN (SELECT Id FROM Categories WHERE Name='Service Outage' LIMIT 1)
      WHEN MOD(t.Id, 100) < 39 THEN (SELECT Id FROM Categories WHERE Name='Slow Speed' LIMIT 1)
      WHEN MOD(t.Id, 100) < 55 THEN (SELECT Id FROM Categories WHERE Name='Router Issues' LIMIT 1)
      WHEN MOD(t.Id, 100) < 71 THEN (SELECT Id FROM Categories WHERE Name='WiFi Problems' LIMIT 1)
      WHEN MOD(t.Id, 100) < 88 THEN (SELECT Id FROM Categories WHERE Name='Billing' LIMIT 1)
      ELSE (SELECT Id FROM Categories WHERE Name='Other' LIMIT 1)
    END,
    ELT(1 + MOD(t.Id, 4), 'Low','Medium','High','Critical'),
    ROUND(0.55 + MOD(t.Id * 17, 450) / 1000, 3),
    ELT(1 + MOD(t.Id, 4), 'v1.0.0','v1.1.0','v1.2.0','v2.0.0'),
    15 + MOD(t.Id * 29, 950),
    MOD(t.Id, 100) < 6,
    t.CreatedAt + INTERVAL (1 + MOD(t.Id, 12)) MINUTE
FROM Tickets t;

-- Three history events for most tickets + one extra event for ~50%.
INSERT INTO TicketHistories
(TicketId, Action, OldValue, NewValue, ChangedById, Notes, CreatedAt)
SELECT t.Id, 'Created', NULL, 'New',
       (SELECT Id FROM Users WHERE Email='admin@isp.com' LIMIT 1),
       'Synthetic initial ticket creation.',
       t.CreatedAt
FROM Tickets t;

INSERT INTO TicketHistories
(TicketId, Action, OldValue, NewValue, ChangedById, Notes, CreatedAt)
SELECT t.Id, 'Assigned', 'Unassigned', 'Assigned',
       COALESCE(t.AssignedToId, (SELECT Id FROM Users WHERE Email='agent@isp.com' LIMIT 1)),
       'Synthetic routing event.',
       COALESCE(t.AssignedAt, t.CreatedAt + INTERVAL 15 MINUTE)
FROM Tickets t
WHERE t.AssignedToId IS NOT NULL;

INSERT INTO TicketHistories
(TicketId, Action, OldValue, NewValue, ChangedById, Notes, CreatedAt)
SELECT t.Id, 'StatusChanged', 'InProgress',
       CASE t.Status
         WHEN 5 THEN 'Resolved'
         WHEN 6 THEN 'Closed'
         WHEN 4 THEN 'PendingCustomer'
         WHEN 7 THEN 'Cancelled'
         ELSE 'InProgress'
       END,
       COALESCE(t.AssignedToId, (SELECT Id FROM Users WHERE Email='agent@isp.com' LIMIT 1)),
       'Synthetic status transition for analytics.',
       COALESCE(t.ResolvedAt, t.UpdatedAt, UTC_TIMESTAMP())
FROM Tickets t
WHERE t.Status <> 0;

INSERT INTO TicketHistories
(TicketId, Action, OldValue, NewValue, ChangedById, Notes, CreatedAt)
SELECT t.Id, 'CustomerContact', NULL, 'Contacted',
       COALESCE(t.AssignedToId, (SELECT Id FROM Users WHERE Email='agent@isp.com' LIMIT 1)),
       'Synthetic customer-contact event.',
       COALESCE(t.FirstResponseAt, t.CreatedAt + INTERVAL 30 MINUTE)
FROM Tickets t
WHERE MOD(t.Id, 2) = 0;

DROP TEMPORARY TABLE IF EXISTS demo_numbers;

-- Recreate reporting views if they are not already present.
-- (Safe because these are the project's own Power BI views.)
CREATE OR REPLACE VIEW vw_PowerBI_Tickets AS
SELECT
    t.Id AS TicketId, t.TicketNumber, t.CustomerId, t.CustomerName, t.CustomerRegion,
    t.Title, t.Description, t.CategoryId,
    COALESCE(c.Name, 'Unclassified') AS Category,
    COALESCE(c.NameAr, 'غير مصنف') AS CategoryAr,
    t.Priority, t.Status, t.Source, t.AssignedToId,
    COALESCE(au.FullName, 'Unassigned') AS AssignedAgent,
    t.TeamId, COALESCE(tm.Name, 'Unassigned') AS Team,
    t.OutageEventId, t.CreatedById, t.CreatedAt, t.ClassifiedAt, t.AssignedAt,
    t.FirstResponseAt, t.ResolvedAt, t.ClosedAt, t.SlaDueAt, t.IsSlaBreached,
    t.SentimentScore, t.AiConfidence, t.UpdatedAt,
    TIMESTAMPDIFF(MINUTE, t.CreatedAt, COALESCE(t.ResolvedAt, UTC_TIMESTAMP())) AS AgeMinutes,
    CASE WHEN t.ResolvedAt IS NOT NULL
         THEN TIMESTAMPDIFF(MINUTE, t.CreatedAt, t.ResolvedAt) END AS ResolutionMinutes,
    CASE WHEN t.FirstResponseAt IS NOT NULL
         THEN TIMESTAMPDIFF(MINUTE, t.CreatedAt, t.FirstResponseAt) END AS FirstResponseMinutes
FROM Tickets t
LEFT JOIN Categories c ON c.Id = t.CategoryId
LEFT JOIN Users au ON au.Id = t.AssignedToId
LEFT JOIN Teams tm ON tm.Id = t.TeamId;

CREATE OR REPLACE VIEW vw_PowerBI_Outages AS
SELECT
    o.Id AS OutageId, o.Title, o.Description, o.Region, o.AffectedCategoryId AS CategoryId,
    COALESCE(c.Name, 'Unclassified') AS Category,
    COALESCE(c.NameAr, 'غير مصنف') AS CategoryAr,
    o.Severity, o.Status, o.DetectedAt, o.ResolvedAt,
    o.EstimatedAffectedCustomers, o.RootCause, o.CreatedBySystem,
    o.CreatedAt, o.UpdatedAt,
    CASE WHEN o.ResolvedAt IS NOT NULL
         THEN TIMESTAMPDIFF(MINUTE, o.DetectedAt, o.ResolvedAt) END AS OutageDurationMinutes
FROM OutageEvents o
LEFT JOIN Categories c ON c.Id = o.AffectedCategoryId;

CREATE OR REPLACE VIEW vw_PowerBI_Classification AS
SELECT
    cl.Id AS ClassificationLogId, cl.TicketId, cl.PredictedCategoryId AS CategoryId,
    COALESCE(c.Name, 'Unclassified') AS PredictedCategory,
    COALESCE(c.NameAr, 'غير مصنف') AS PredictedCategoryAr,
    cl.PredictedPriority, cl.ConfidenceScore, cl.ModelVersion,
    cl.ProcessingTimeMs, cl.WasOverridden, cl.CreatedAt
FROM ClassificationLogs cl
LEFT JOIN Categories c ON c.Id = cl.PredictedCategoryId;

CREATE OR REPLACE VIEW vw_PowerBI_TicketHistory AS
SELECT
    h.Id AS HistoryId, h.TicketId, t.TicketNumber, h.Action, h.OldValue, h.NewValue,
    h.ChangedById, COALESCE(u.FullName, 'System') AS ChangedBy, h.Notes, h.CreatedAt
FROM TicketHistories h
JOIN Tickets t ON t.Id = h.TicketId
LEFT JOIN Users u ON u.Id = h.ChangedById;

CREATE OR REPLACE VIEW vw_PowerBI_Agents AS
SELECT
    u.Id AS AgentId, u.EmployeeCode, u.FullName, u.Email, u.TeamId,
    COALESCE(t.Name, 'Unassigned') AS Team, u.MaxConcurrentTickets, u.IsActive
FROM Users u
JOIN Roles r ON r.Id = u.RoleId
LEFT JOIN Teams t ON t.Id = u.TeamId
WHERE r.Name = 'Agent';

SELECT
  (SELECT COUNT(*) FROM Tickets) AS Tickets,
  (SELECT COUNT(*) FROM OutageEvents) AS Outages,
  (SELECT COUNT(*) FROM ClassificationLogs) AS Classifications,
  (SELECT COUNT(*) FROM TicketHistories) AS HistoryRows;
