"use client";

import { RequireAuth } from "@/components/auth/RequireAuth";
import { ExecutionStatusBadge } from "@/components/executions/ExecutionStatusBadge";
import { getDashboardSummary, getProfile, getSystemHealth } from "@/lib/api";
import type { DashboardSummary, ExecutionStatus, SystemHealth } from "@/types";
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";

type Range = "24h" | "7d" | "30d";

export default function DashboardPage() {
  return (
    <RequireAuth>
      <DashboardContent />
    </RequireAuth>
  );
}

function DashboardContent() {
  const [range, setRange] = useState<Range>("24h");
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [systemHealth, setSystemHealth] = useState<SystemHealth | null>(null);
  const [isAdmin, setIsAdmin] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [profile, dashboard] = await Promise.all([
        getProfile(),
        getDashboardSummary(range),
      ]);
      setIsAdmin(profile.isAdmin);
      setSummary(dashboard);

      if (profile.isAdmin) {
        try {
          const health = await getSystemHealth();
          setSystemHealth(health);
        } catch {
          setSystemHealth(null);
        }
      } else {
        setSystemHealth(null);
      }
    } catch {
      setError("Failed to load dashboard.");
    } finally {
      setLoading(false);
    }
  }, [range]);

  useEffect(() => {
    load();
  }, [load]);

  if (loading && !summary) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-10 text-slate-500">
        Loading dashboard...
      </div>
    );
  }

  if (error || !summary) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-10">
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error ?? "Dashboard unavailable."}
        </div>
      </div>
    );
  }

  const statusItems: { label: string; value: number; color: string }[] = [
    { label: "Succeeded", value: summary.executions.succeeded, color: "bg-green-500" },
    { label: "Failed", value: summary.executions.failed, color: "bg-red-500" },
    { label: "Retrying", value: summary.executions.retrying, color: "bg-yellow-500" },
    { label: "Running", value: summary.executions.running, color: "bg-blue-500" },
    { label: "Queued", value: summary.executions.queued, color: "bg-slate-400" },
    { label: "Cancelled", value: summary.executions.cancelled, color: "bg-slate-300" },
  ];
  const maxStatus = Math.max(...statusItems.map((s) => s.value), 1);

  return (
    <div className="mx-auto max-w-6xl px-4 py-10">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Dashboard</h1>
          <p className="mt-1 text-slate-600">
            Operational overview for your jobs and executions.
          </p>
        </div>
        <div className="flex gap-2">
          {(["24h", "7d", "30d"] as Range[]).map((r) => (
            <button
              key={r}
              type="button"
              onClick={() => setRange(r)}
              className={`rounded-lg px-3 py-1.5 text-sm font-medium ${
                range === r
                  ? "bg-brand-600 text-white"
                  : "border border-slate-300 text-slate-700 hover:bg-slate-50"
              }`}
            >
              {r}
            </button>
          ))}
        </div>
      </div>

      <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
        <StatCard label="Total Jobs" value={summary.jobs.total} />
        <StatCard label="Active Jobs" value={summary.jobs.active} />
        <StatCard label="Running" value={summary.executions.running} />
        <StatCard label="Retrying" value={summary.executions.retrying} />
        <StatCard label="Failed" value={summary.executions.failed} />
        <StatCard label="Succeeded" value={summary.executions.succeeded} />
      </div>

      <div className="mt-8 grid gap-6 lg:grid-cols-2">
        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <h2 className="text-lg font-semibold text-slate-900">Execution Health</h2>
          <p className="mt-1 text-sm text-slate-500">
            Success rate:{" "}
            {summary.rates.successRate != null
              ? `${(summary.rates.successRate * 100).toFixed(1)}%`
              : "—"}{" "}
            ({summary.rates.completedCount} completed in {summary.range})
          </p>
          <div className="mt-6 space-y-3">
            {statusItems.map((item) => (
              <div key={item.label}>
                <div className="mb-1 flex justify-between text-sm">
                  <span className="text-slate-600">{item.label}</span>
                  <span className="font-medium text-slate-900">{item.value}</span>
                </div>
                <div className="h-2 rounded-full bg-slate-100">
                  <div
                    className={`h-2 rounded-full ${item.color}`}
                    style={{ width: `${(item.value / maxStatus) * 100}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
          {summary.duration.averageMs != null && (
            <p className="mt-4 text-sm text-slate-600">
              Avg duration: {Math.round(summary.duration.averageMs)} ms
              {summary.duration.minMs != null && summary.duration.maxMs != null
                ? ` (min ${Math.round(summary.duration.minMs)} / max ${Math.round(summary.duration.maxMs)})`
                : ""}
            </p>
          )}
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <h2 className="text-lg font-semibold text-slate-900">Retry Metrics</h2>
          <dl className="mt-4 grid gap-3 sm:grid-cols-3">
            <MetricItem label="Currently retrying" value={summary.retries.currentlyRetrying} />
            <MetricItem label="Retries in range" value={summary.retries.retriesInRange} />
            <MetricItem
              label="Succeeded after retry"
              value={summary.retries.succeededAfterRetryInRange}
            />
          </dl>
          <div className="mt-6 grid gap-3 sm:grid-cols-2">
            <MetricItem label="Paused jobs" value={summary.jobs.paused} />
            <MetricItem label="Archived jobs" value={summary.jobs.archived} />
          </div>
        </div>
      </div>

      {isAdmin && systemHealth && (
        <div className="mt-8 rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <h2 className="text-lg font-semibold text-slate-900">System Health (Admin)</h2>
          <p className="mt-1 text-sm text-slate-500">Overall: {systemHealth.status}</p>
          <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <HealthPill label="Database" status={systemHealth.database.status} />
            <HealthPill label="Hangfire" status={systemHealth.hangfire.status} />
            <HealthPill label="Outbox" status={systemHealth.outbox.status} />
            <HealthPill label="Workers" status={systemHealth.workers.status} />
          </div>
          <div className="mt-4 text-sm text-slate-600">
            Outbox: {systemHealth.outbox.pending} pending, {systemHealth.outbox.processing}{" "}
            processing, {systemHealth.outbox.failed} failed
            {systemHealth.outbox.oldestPendingAgeMinutes != null &&
              ` · oldest pending ${systemHealth.outbox.oldestPendingAgeMinutes} min`}
          </div>
          {systemHealth.workers.items.length > 0 && (
            <ul className="mt-4 space-y-2 text-sm text-slate-700">
              {systemHealth.workers.items.map((w) => (
                <li key={w.workerId} className="rounded-lg bg-slate-50 px-3 py-2">
                  {w.hostName} · {w.status} · heartbeat {w.secondsSinceHeartbeat}s ago
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      <div className="mt-8 grid gap-6 lg:grid-cols-2">
        <ActivityTable
          title="Recent Executions"
          rows={summary.recentExecutions.map((e) => ({
            id: e.id,
            jobName: e.jobName,
            status: e.status,
            meta: `Attempt ${e.attempt} · ${formatTime(e.createdAtUtc)}`,
          }))}
        />
        <ActivityTable
          title="Recent Failures"
          rows={summary.recentFailures.map((f) => ({
            id: f.id,
            jobName: f.jobName,
            status: "Failed" as ExecutionStatus,
            meta: f.errorMessage ?? `HTTP ${f.httpStatusCode ?? "?"}`,
          }))}
        />
      </div>

      <div className="mt-8">
        <Link
          href="/jobs"
          className="rounded-lg bg-brand-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-brand-700"
        >
          Manage Jobs
        </Link>
      </div>
    </div>
  );
}

function StatCard({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <p className="text-sm text-slate-500">{label}</p>
      <p className="mt-1 text-2xl font-bold text-slate-900">{value}</p>
    </div>
  );
}

function MetricItem({ label, value }: { label: string; value: number }) {
  return (
    <div>
      <dt className="text-xs font-medium uppercase text-slate-500">{label}</dt>
      <dd className="mt-1 text-xl font-semibold text-slate-900">{value}</dd>
    </div>
  );
}

function HealthPill({ label, status }: { label: string; status: string }) {
  const color =
    status === "Healthy"
      ? "bg-green-100 text-green-800"
      : status === "Degraded"
        ? "bg-yellow-100 text-yellow-800"
        : "bg-red-100 text-red-800";

  return (
    <div className={`rounded-lg px-3 py-2 text-sm font-medium ${color}`}>
      {label}: {status}
    </div>
  );
}

function ActivityTable({
  title,
  rows,
}: {
  title: string;
  rows: Array<{ id: string; jobName: string; status: ExecutionStatus; meta: string }>;
}) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
      <h2 className="text-lg font-semibold text-slate-900">{title}</h2>
      {rows.length === 0 ? (
        <p className="mt-4 text-sm text-slate-500">No activity yet.</p>
      ) : (
        <ul className="mt-4 divide-y divide-slate-100">
          {rows.map((row) => (
            <li key={row.id} className="py-3">
              <Link href={`/executions/${row.id}`} className="block hover:bg-slate-50 -mx-2 px-2 rounded">
                <div className="flex items-center justify-between gap-2">
                  <span className="font-medium text-slate-900 truncate">{row.jobName}</span>
                  <ExecutionStatusBadge status={row.status} />
                </div>
                <p className="mt-1 truncate text-xs text-slate-500">{row.meta}</p>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function formatTime(value: string) {
  return new Date(value).toLocaleString();
}
