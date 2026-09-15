"use client";

import { RequireAuth } from "@/components/auth/RequireAuth";
import { ExecutionStatusBadge } from "@/components/executions/ExecutionStatusBadge";
import { ApiError, cancelExecution, getExecution, retryExecution } from "@/lib/api";
import type { ExecutionDetail, ExecutionStatus } from "@/types";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useCallback, useEffect, useState } from "react";

const POLL_INTERVAL_MS = 2000;
const POLLING: ExecutionStatus[] = ["Queued", "Running", "Retrying"];

function formatTriggerType(triggerType: string) {
  if (triggerType === "ManualRetry") return "Manual Retry";
  return triggerType;
}

export default function ExecutionDetailPage() {
  return (
    <RequireAuth>
      <ExecutionDetailContent />
    </RequireAuth>
  );
}

function ExecutionDetailContent() {
  const params = useParams();
  const executionId = params.id as string;

  const [execution, setExecution] = useState<ExecutionDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [showCancelConfirm, setShowCancelConfirm] = useState(false);

  const load = useCallback(async () => {
    try {
      const data = await getExecution(executionId);
      setExecution(data);
      setError(null);
      return data;
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) {
        setError("Execution not found.");
      } else if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Failed to load execution.");
      }
      return null;
    } finally {
      setLoading(false);
    }
  }, [executionId]);

  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setInterval> | null = null;

    const poll = async () => {
      const data = await load();
      if (cancelled || !data) return;

      if (POLLING.includes(data.status)) {
        timer = setTimeout(poll, POLL_INTERVAL_MS);
      }
    };

    poll();

    return () => {
      cancelled = true;
      if (timer) clearTimeout(timer);
    };
  }, [load]);

  const handleRetry = async () => {
    if (!execution || actionLoading) return;

    setActionLoading(true);
    setError(null);

    try {
      const idempotencyKey = crypto.randomUUID();
      await retryExecution(execution.id, idempotencyKey);
      await load();
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Failed to retry execution.");
      }
    } finally {
      setActionLoading(false);
    }
  };

  const handleCancel = async () => {
    if (!execution || actionLoading) return;

    setActionLoading(true);
    setError(null);
    setShowCancelConfirm(false);

    try {
      const updated = await cancelExecution(execution.id);
      setExecution(updated);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Failed to cancel execution.");
      }
    } finally {
      setActionLoading(false);
    }
  };

  if (loading && !execution) {
    return (
      <div className="mx-auto max-w-4xl px-4 py-10 text-slate-500">
        Loading execution...
      </div>
    );
  }

  if (error && !execution) {
    return (
      <div className="mx-auto max-w-4xl px-4 py-10">
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error ?? "Execution not found."}
        </div>
        <Link href="/jobs" className="mt-4 inline-block text-sm text-brand-600">
          ← Back to jobs
        </Link>
      </div>
    );
  }

  if (!execution) {
    return null;
  }

  const canRetry = execution.status === "Failed";
  const canCancel =
    execution.status === "Queued" || execution.status === "Retrying";

  return (
    <div className="mx-auto max-w-4xl px-4 py-10">
      <div className="mb-6">
        <Link
          href={`/jobs/${execution.jobId}/executions`}
          className="text-sm text-brand-600 hover:text-brand-700"
        >
          ← Back to execution history
        </Link>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">
              Execution #{execution.id.slice(0, 8)}
            </h1>
            <p className="mt-1 text-slate-600">Job: {execution.jobName}</p>
          </div>
          <div className="flex flex-col items-end gap-2">
            <ExecutionStatusBadge status={execution.status} />
            <div className="flex gap-2">
              {canRetry && (
                <button
                  type="button"
                  onClick={handleRetry}
                  disabled={actionLoading}
                  className="rounded-lg bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50"
                >
                  Retry
                </button>
              )}
              {canCancel && (
                <button
                  type="button"
                  onClick={() => setShowCancelConfirm(true)}
                  disabled={actionLoading}
                  className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                >
                  Cancel
                </button>
              )}
            </div>
          </div>
        </div>

        {error && (
          <div className="mt-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {error}
          </div>
        )}

        {showCancelConfirm && (
          <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3">
            <p className="text-sm text-amber-900">Cancel this execution?</p>
            <div className="mt-3 flex gap-2">
              <button
                type="button"
                onClick={handleCancel}
                disabled={actionLoading}
                className="rounded-lg bg-red-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
              >
                Yes, cancel
              </button>
              <button
                type="button"
                onClick={() => setShowCancelConfirm(false)}
                disabled={actionLoading}
                className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
              >
                No, keep running
              </button>
            </div>
          </div>
        )}

        <dl className="mt-6 grid gap-4 sm:grid-cols-2">
          <DetailItem label="Trigger" value={formatTriggerType(execution.triggerType)} />
          <DetailItem
            label="Attempt"
            value={`${execution.attempt} of ${execution.maxRetries + 1}`}
          />
          {execution.status === "Retrying" && execution.nextRetryAtUtc && (
            <DetailItem
              label="Next retry"
              value={formatTime(execution.nextRetryAtUtc)}
            />
          )}
          <DetailItem
            label="Started"
            value={formatTime(execution.startedAtUtc)}
          />
          <DetailItem
            label="Completed"
            value={formatTime(execution.completedAtUtc)}
          />
          <DetailItem
            label="Duration"
            value={
              execution.durationMs != null
                ? `${execution.durationMs} ms`
                : "—"
            }
          />
          <DetailItem
            label="HTTP Status"
            value={
              execution.httpStatusCode != null
                ? String(execution.httpStatusCode)
                : "—"
            }
          />
        </dl>

        {execution.errorMessage && (
          <div className="mt-6 rounded-lg border border-red-200 bg-red-50 px-4 py-3">
            <p className="text-sm font-medium text-red-800">Error</p>
            <p className="mt-1 text-sm text-red-700">{execution.errorMessage}</p>
          </div>
        )}

        {execution.responseBody && (
          <div className="mt-6">
            <p className="text-sm font-medium text-slate-700">Response</p>
            <pre className="mt-2 max-h-64 overflow-auto rounded-lg bg-slate-50 p-4 text-xs text-slate-800">
              {execution.responseBody}
            </pre>
          </div>
        )}

        {execution.logs.length > 0 && (
          <div className="mt-6">
            <p className="text-sm font-medium text-slate-700">Logs</p>
            <ul className="mt-2 space-y-1 rounded-lg bg-slate-50 p-4 font-mono text-xs text-slate-700">
              {execution.logs.map((log) => (
                <li key={log.id}>
                  {formatTime(log.createdAtUtc)} [{log.level}] {log.message}
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  );
}

function DetailItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-medium uppercase text-slate-500">{label}</dt>
      <dd className="mt-1 text-sm text-slate-900">{value}</dd>
    </div>
  );
}

function formatTime(value?: string) {
  if (!value) return "—";
  return new Date(value).toLocaleString();
}
