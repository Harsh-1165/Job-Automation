import { clearAuth, getAccessToken } from "./auth";

const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
    public readonly fields?: Record<string, string[]>
  ) {
    super(message);
    this.name = "ApiError";
  }
}

interface ErrorResponseBody {
  error?: {
    code?: string;
    message?: string;
    fields?: Record<string, string[]>;
  };
}

async function parseErrorResponse(response: Response): Promise<ApiError> {
  try {
    const body = (await response.json()) as ErrorResponseBody;
    return new ApiError(
      response.status,
      body.error?.code ?? "UNKNOWN_ERROR",
      body.error?.message ?? response.statusText,
      body.error?.fields
    );
  } catch {
    return new ApiError(
      response.status,
      "UNKNOWN_ERROR",
      response.statusText || "Request failed"
    );
  }
}

export async function apiRequest<T>(
  path: string,
  options: RequestInit = {},
  requireAuth = false
): Promise<T> {
  const url = `${API_BASE_URL}${path.startsWith("/") ? path : `/${path}`}`;

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string>),
  };

  if (requireAuth) {
    const token = getAccessToken();
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }
  }

  const response = await fetch(url, {
    ...options,
    headers,
  });

  if (response.status === 401) {
    clearAuth();
    if (typeof window !== "undefined" && !path.includes("/auth/")) {
      window.location.href = "/login";
    }
  }

  if (!response.ok) {
    throw await parseErrorResponse(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

// --- Health ---

export interface HealthResponse {
  status: string;
  database?: string;
  hangfire?: string;
}

export async function getHealth(): Promise<HealthResponse> {
  return apiRequest<HealthResponse>("/health");
}

export async function getHealthLive(): Promise<{ status: string }> {
  return apiRequest<{ status: string }>("/health/live");
}

export async function getHealthReady(): Promise<{
  status: string;
  database: boolean;
  hangfire: boolean;
}> {
  return apiRequest("/health/ready");
}

// --- Auth ---

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
}

export async function register(
  email: string,
  password: string
): Promise<AuthResponse> {
  return apiRequest<AuthResponse>("/api/auth/register", {
    method: "POST",
    body: JSON.stringify({ email, password }),
  });
}

export async function login(
  email: string,
  password: string
): Promise<AuthResponse> {
  return apiRequest<AuthResponse>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password }),
  });
}

export async function getProfile(): Promise<import("@/types").UserProfile> {
  return apiRequest<import("@/types").UserProfile>("/api/auth/me", {}, true);
}

// --- Jobs ---

import type {
  DashboardSummary,
  ExecutionDetail,
  Job,
  JobStatus,
  PagedExecutionsResponse,
  PagedJobsResponse,
  RunJobResponse,
  SystemHealth,
} from "@/types";

export interface CreateJobPayload {
  name: string;
  description?: string;
  url: string;
  httpMethod: string;
  headers?: Record<string, string>;
  body?: string;
  schedule?: string;
  timeoutSeconds: number;
}

export type UpdateJobPayload = CreateJobPayload;

export async function listJobs(params: {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: JobStatus;
  includeArchived?: boolean;
}): Promise<PagedJobsResponse> {
  const query = new URLSearchParams();
  if (params.page) query.set("page", String(params.page));
  if (params.pageSize) query.set("pageSize", String(params.pageSize));
  if (params.search) query.set("search", params.search);
  if (params.status) query.set("status", params.status);
  if (params.includeArchived) query.set("includeArchived", "true");

  const qs = query.toString();
  return apiRequest<PagedJobsResponse>(
    `/api/jobs${qs ? `?${qs}` : ""}`,
    {},
    true
  );
}

export async function getJob(id: string): Promise<Job> {
  return apiRequest<Job>(`/api/jobs/${id}`, {}, true);
}

export async function createJob(payload: CreateJobPayload): Promise<Job> {
  return apiRequest<Job>(
    "/api/jobs",
    { method: "POST", body: JSON.stringify(payload) },
    true
  );
}

export async function updateJob(
  id: string,
  payload: UpdateJobPayload
): Promise<Job> {
  return apiRequest<Job>(
    `/api/jobs/${id}`,
    { method: "PUT", body: JSON.stringify(payload) },
    true
  );
}

export async function archiveJob(id: string): Promise<void> {
  return apiRequest<void>(
    `/api/jobs/${id}`,
    { method: "DELETE" },
    true
  );
}

export async function enableJob(id: string): Promise<Job> {
  return apiRequest<Job>(
    `/api/jobs/${id}/enable`,
    { method: "POST" },
    true
  );
}

export async function disableJob(id: string): Promise<Job> {
  return apiRequest<Job>(
    `/api/jobs/${id}/disable`,
    { method: "POST" },
    true
  );
}

export async function runJob(
  id: string,
  idempotencyKey: string
): Promise<RunJobResponse> {
  return apiRequest<RunJobResponse>(
    `/api/jobs/${id}/run`,
    {
      method: "POST",
      headers: { "Idempotency-Key": idempotencyKey },
    },
    true
  );
}

export async function listExecutions(
  jobId: string,
  params: { page?: number; pageSize?: number } = {}
): Promise<PagedExecutionsResponse> {
  const query = new URLSearchParams();
  if (params.page) query.set("page", String(params.page));
  if (params.pageSize) query.set("pageSize", String(params.pageSize));

  const qs = query.toString();
  return apiRequest<PagedExecutionsResponse>(
    `/api/jobs/${jobId}/executions${qs ? `?${qs}` : ""}`,
    {},
    true
  );
}

export async function getExecution(id: string): Promise<ExecutionDetail> {
  return apiRequest<ExecutionDetail>(`/api/executions/${id}`, {}, true);
}

export async function retryExecution(
  id: string,
  idempotencyKey: string
): Promise<RunJobResponse> {
  return apiRequest<RunJobResponse>(
    `/api/executions/${id}/retry`,
    {
      method: "POST",
      headers: { "Idempotency-Key": idempotencyKey },
    },
    true
  );
}

export async function cancelExecution(id: string): Promise<ExecutionDetail> {
  return apiRequest<ExecutionDetail>(
    `/api/executions/${id}/cancel`,
    { method: "POST" },
    true
  );
}

export async function getDashboardSummary(
  range: "24h" | "7d" | "30d" = "24h"
): Promise<DashboardSummary> {
  return apiRequest<DashboardSummary>(
    `/api/dashboard/summary?range=${range}`,
    {},
    true
  );
}

export async function getSystemHealth(): Promise<SystemHealth> {
  return apiRequest<SystemHealth>("/api/admin/system", {}, true);
}
