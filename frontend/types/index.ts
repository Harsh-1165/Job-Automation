export type JobStatus = "Draft" | "Active" | "Paused" | "Archived";

export type ExecutionStatus =
  | "Queued"
  | "Running"
  | "Succeeded"
  | "Failed"
  | "Retrying"
  | "Cancelled";

export type TriggerType = "Manual" | "Scheduled" | "Retry" | "ManualRetry";

export interface Job {
  id: string;
  name: string;
  description?: string;
  url: string;
  httpMethod: string;
  headers?: Record<string, string>;
  body?: string;
  schedule?: string;
  status: JobStatus;
  timeoutSeconds: number;
  createdAt: string;
  updatedAt: string;
  lastRunAt?: string;
  nextRunAt?: string;
}

export interface PagedJobsResponse {
  items: Job[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface RunJobResponse {
  id: string;
  jobId: string;
  status: ExecutionStatus;
  triggerType: TriggerType;
  attempt: number;
  createdAtUtc: string;
  isNewlyCreated: boolean;
}

export interface ExecutionSummary {
  id: string;
  jobId: string;
  status: ExecutionStatus;
  triggerType: TriggerType;
  attempt: number;
  httpStatusCode?: number;
  durationMs?: number;
  createdAtUtc: string;
}

export interface ExecutionLog {
  id: string;
  level: string;
  message: string;
  createdAtUtc: string;
}

export interface ExecutionDetail {
  id: string;
  jobId: string;
  jobName: string;
  status: ExecutionStatus;
  triggerType: TriggerType;
  attempt: number;
  maxRetries: number;
  nextRetryAtUtc?: string;
  startedAtUtc?: string;
  completedAtUtc?: string;
  durationMs?: number;
  httpStatusCode?: number;
  responseBody?: string;
  errorMessage?: string;
  createdAtUtc: string;
  logs: ExecutionLog[];
}

export interface PagedExecutionsResponse {
  items: ExecutionSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface UserProfile {
  userId: string;
  email: string;
  isAdmin: boolean;
}

export interface DashboardSummary {
  range: string;
  jobs: {
    total: number;
    active: number;
    paused: number;
    archived: number;
  };
  executions: {
    total: number;
    queued: number;
    running: number;
    retrying: number;
    succeeded: number;
    failed: number;
    cancelled: number;
  };
  rates: {
    successRate?: number;
    failureRate?: number;
    completedCount: number;
  };
  duration: {
    averageMs?: number;
    minMs?: number;
    maxMs?: number;
  };
  recentExecutions: Array<{
    id: string;
    jobId: string;
    jobName: string;
    status: ExecutionStatus;
    triggerType: TriggerType;
    attempt: number;
    createdAtUtc: string;
    completedAtUtc?: string;
    durationMs?: number;
  }>;
  recentFailures: Array<{
    id: string;
    jobId: string;
    jobName: string;
    httpStatusCode?: number;
    errorMessage?: string;
    attempt: number;
    completedAtUtc: string;
  }>;
  retries: {
    currentlyRetrying: number;
    retriesInRange: number;
    succeededAfterRetryInRange: number;
  };
}

export interface SystemHealth {
  status: string;
  database: { status: string };
  hangfire: { status: string };
  outbox: {
    status: string;
    pending: number;
    processing: number;
    failed: number;
    oldestPendingAgeMinutes?: number;
  };
  workers: {
    status: string;
    total: number;
    healthy: number;
    stale: number;
    items: Array<{
      workerId: string;
      hostName: string;
      status: string;
      lastHeartbeatAtUtc: string;
      secondsSinceHeartbeat: number;
      lastProcessedAtUtc?: string;
    }>;
  };
}
