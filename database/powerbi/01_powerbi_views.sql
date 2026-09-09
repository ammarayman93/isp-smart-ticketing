-- Power BI reporting layer for ISP Smart Ticketing.
-- Run this script against the isp_ticketing database after migrations.
-- The views intentionally expose analytics-friendly fields and keep the
-- operational tables unchanged.

CREATE OR REPLACE VIEW vw_PowerBI_Tickets AS
SELECT
    t.Id AS TicketId,
    t.TicketNumber,
    t.CustomerId,
    t.CustomerName,
    t.CustomerRegion,
    t.Title,
    t.Description,
    t.CategoryId,
    COALESCE(c.Name, 'Unclassified') AS Category,
    COALESCE(c.NameAr, 'غير مصنف') AS CategoryAr,
    t.Priority,
    t.Status,
    t.Source,
    t.AssignedToId,
    COALESCE(au.FullName, 'Unassigned') AS AssignedAgent,
    t.TeamId,
    COALESCE(tm.Name, 'Unassigned') AS Team,
    t.OutageEventId,
    t.CreatedById,
    t.CreatedAt,
    t.ClassifiedAt,
    t.AssignedAt,
    t.FirstResponseAt,
    t.ResolvedAt,
    t.ClosedAt,
    t.SlaDueAt,
    t.IsSlaBreached,
    t.SentimentScore,
    t.AiConfidence,
    t.UpdatedAt,
    TIMESTAMPDIFF(MINUTE, t.CreatedAt, COALESCE(t.ResolvedAt, UTC_TIMESTAMP())) AS AgeMinutes,
    CASE WHEN t.ResolvedAt IS NOT NULL THEN TIMESTAMPDIFF(MINUTE, t.CreatedAt, t.ResolvedAt) END AS ResolutionMinutes,
    CASE WHEN t.FirstResponseAt IS NOT NULL THEN TIMESTAMPDIFF(MINUTE, t.CreatedAt, t.FirstResponseAt) END AS FirstResponseMinutes
FROM Tickets t
LEFT JOIN Categories c ON c.Id = t.CategoryId
LEFT JOIN Users au ON au.Id = t.AssignedToId
LEFT JOIN Teams tm ON tm.Id = t.TeamId;

CREATE OR REPLACE VIEW vw_PowerBI_Outages AS
SELECT
    o.Id AS OutageId,
    o.Title,
    o.Description,
    o.Region,
    o.AffectedCategoryId AS CategoryId,
    COALESCE(c.Name, 'Unclassified') AS Category,
    COALESCE(c.NameAr, 'غير مصنف') AS CategoryAr,
    o.Severity,
    o.Status,
    o.DetectedAt,
    o.ResolvedAt,
    o.EstimatedAffectedCustomers,
    o.RootCause,
    o.CreatedBySystem,
    o.CreatedAt,
    o.UpdatedAt,
    CASE WHEN o.ResolvedAt IS NOT NULL THEN TIMESTAMPDIFF(MINUTE, o.DetectedAt, o.ResolvedAt) END AS OutageDurationMinutes
FROM OutageEvents o
LEFT JOIN Categories c ON c.Id = o.AffectedCategoryId;

CREATE OR REPLACE VIEW vw_PowerBI_Classification AS
SELECT
    cl.Id AS ClassificationLogId,
    cl.TicketId,
    cl.PredictedCategoryId AS CategoryId,
    COALESCE(c.Name, 'Unclassified') AS PredictedCategory,
    COALESCE(c.NameAr, 'غير مصنف') AS PredictedCategoryAr,
    cl.PredictedPriority,
    cl.ConfidenceScore,
    cl.ModelVersion,
    cl.ProcessingTimeMs,
    cl.WasOverridden,
    cl.CreatedAt
FROM ClassificationLogs cl
LEFT JOIN Categories c ON c.Id = cl.PredictedCategoryId;

CREATE OR REPLACE VIEW vw_PowerBI_TicketHistory AS
SELECT
    h.Id AS HistoryId,
    h.TicketId,
    t.TicketNumber,
    h.Action,
    h.OldValue,
    h.NewValue,
    h.ChangedById,
    COALESCE(u.FullName, 'System') AS ChangedBy,
    h.Notes,
    h.CreatedAt
FROM TicketHistories h
JOIN Tickets t ON t.Id = h.TicketId
LEFT JOIN Users u ON u.Id = h.ChangedById;

CREATE OR REPLACE VIEW vw_PowerBI_Agents AS
SELECT
    u.Id AS AgentId,
    u.EmployeeCode,
    u.FullName,
    u.Email,
    u.TeamId,
    COALESCE(t.Name, 'Unassigned') AS Team,
    u.MaxConcurrentTickets,
    u.IsActive
FROM Users u
JOIN Roles r ON r.Id = u.RoleId
LEFT JOIN Teams t ON t.Id = u.TeamId
WHERE r.Name = 'Agent';
