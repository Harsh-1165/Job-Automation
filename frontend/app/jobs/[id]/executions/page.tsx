"use client";

import { RequireAuth } from "@/components/auth/RequireAuth";
import { ExecutionStatusBadge } from "@/components/executions/ExecutionStatusBadge";
import { listExecutions } from "@/lib/api";
import type { ExecutionSummary, TriggerType } from "@/types";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useCallback, useEffect, useState } from "react";

export default function JobExecutionsPage() {
  return (
    <RequireAuth>
      <JobExecutionsContent />
    </RequireAuth>
  );
}

function JobExecutionsContent() {
  const params = useParams();
  const jobId = params.id as string;

  const [executions, setExecutions] = useState<ExecutionSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await listExecutions(jobId);
      setExecutions(result.items);
    } catch (err: unknown) {
      if (err && typeof err === "object" && "message" in err) {
        setError(String(err.message));
      } else {
        setError("Failed to load executions.");
      }
    } finally {
      setLoading(false);
    }
  }, [jobId]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div className="mx-auto max-w-5xl px-4 py-10">
      <Link href="/jobs" className="text-sm text-brand-600 hover:text-brand-700">
        ← Back to jobs
      </Link>

      <h1 className="mt-4 text-2xl font-bold text-slate-900">Execution History</h1>
      <p className="mt-1 text-slate-600">Job ID: {jobId}</p>

      {error && (
        <div className="mt-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      {loading ? (
        <div className="mt-8 text-center text-slate-500">Loading...</div>
      ) : executions.length === 0 ? (
        <div className="mt-8 rounded-xl border border-dashed border-slate-300 bg-white p-12 text-center">
          <p className="text-slate-500">No executions yet. Use Run Now on the jobs page.</p>
        </div>
      ) : (
        <div className="mt-8 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="min-w-full divide-y divide-slate-200">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-slate-500">
                  Execution
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-slate-500">
                  Status
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-slate-500">
                  Trigger
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-slate-500">
                  Duration
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-slate-500">
                  HTTP
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-slate-500">
                  Created
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200">
              {executions.map((ex) => (
                <tr key={ex.id} className="hover:bg-slate-50">
                  <td className="px-4 py-3 text-sm">
                    <Link
                      href={`/executions/${ex.id}`}
                      className="font-medium text-brand-600 hover:text-brand-700"
                    >
                      #{ex.id.slice(0, 8)}
                    </Link>
                  </td>
                  <td className="px-4 py-3">
                    <ExecutionStatusBadge status={ex.status} />
                  </td>
                  <td className="px-4 py-3 text-sm">
                    <TriggerBadge trigger={ex.triggerType} />
                  </td>
                  <td className="px-4 py-3 text-sm text-slate-600">
                    {ex.durationMs != null ? `${ex.durationMs} ms` : "—"}
                  </td>
                  <td className="px-4 py-3 text-sm text-slate-600">
                    {ex.httpStatusCode ?? "—"}
                  </td>
                  <td className="px-4 py-3 text-sm text-slate-600">
                    {new Date(ex.createdAtUtc).toLocaleString()}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function TriggerBadge({ trigger }: { trigger: TriggerType }) {
  const styles: Record<TriggerType, string> = {
    Manual: "bg-blue-100 text-blue-800",
    Scheduled: "bg-purple-100 text-purple-800",
    Retry: "bg-orange-100 text-orange-800",
    ManualRetry: "bg-teal-100 text-teal-800",
  };

  const labels: Record<TriggerType, string> = {
    Manual: "Manual",
    Scheduled: "Scheduled",
    Retry: "Retry",
    ManualRetry: "Manual Retry",
  };

  return (
    <span
      className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${styles[trigger]}`}
    >
      {labels[trigger]}
    </span>
  );
}
