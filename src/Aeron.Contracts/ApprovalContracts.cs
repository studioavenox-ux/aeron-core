namespace Aeron.Contracts;

// AERON Milestone 2 moved the approval contracts into the durable kernel
// (Aeron.Contracts/ApprovalStoreContracts.cs and DurableAuditContracts.cs).
//
// The volatile M1/M2 prototype types (ApprovalStatus, ApprovalEventKind,
// IApprovalAuditSink, ApprovalResult, ApprovalResultCode, OwnerApprovalService) now live
// with their prototype implementation in Aeron.Security/LegacyApprovalPrototype.cs so the
// production contract surface cannot reference them by accident.
