"use client";

import { RequireAuth } from "@/components/auth/RequireAuth";
import { JobForm } from "@/components/jobs/JobForm";
import {
  archiveJob,
  createJob,
  disableJob,
  enableJob,
  listJobs,
  runJob,
  updateJob,
  type CreateJobPayload,
} from "@/lib/api";
import type { Job, JobStatus } from "@/types";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";

const STATUS_OPTIONS: { value: string; label: string }[] = [
  { value: "", label: "All" },
  { value: "Active", label: "Active" },
  { value: "Paused", label: "Paused" },
  { value: "Draft", label: "Draft" },
];

export default function JobsPage() {
  return (
    <RequireAuth>
      <JobsContent />
    </RequireAuth>
  );
}

function JobsContent() {
  const router = useRouter();
  const [jobs, setJobs] = useState<Job[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [showForm, setShowForm] = useState(false);
  const [editingJob, setEditingJob] = useState<Job | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const runNowKeysRef = useRef<Map<string, string>>(new Map());

  const loadJobs = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await listJobs({
        search: search || undefined,
        status: (statusFilter as JobStatus) || undefined,
      });
      setJobs(result.items);
    } catch (err: unknown) {
      if (err && typeof err === "object" && "message" in err) {
        setError(String(err.message));
      } else {
        setError("Failed to load jobs.");
      }
    } finally {
      setLoading(false);
    }
  }, [search, statusFilter]);

  useEffect(() => {
    const timer = setTimeout(loadJobs, 300);
    return () => clearTimeout(timer);
  }, [loadJobs]);

  const handleCreate = async (payload: CreateJobPayload) => {
    await createJob(payload);
    setShowForm(false);
    await loadJobs();
  };

  const handleUpdate = async (payload: CreateJobPayload) => {
    if (!editingJob) return;
    await updateJob(editingJob.id, payload);
    setEditingJob(null);
    await loadJobs();
  };

  const handleArchive = async (job: Job) => {
    if (!confirm(`Archive job "${job.name}"?`)) return;
    setActionLoading(job.id);
    try {
      await archiveJob(job.id);
      await loadJobs();
    } catch (err: unknown) {
      if (err && typeof err === "object" && "message" in err) {
        alert(String(err.message));
      }
    } finally {
      setActionLoading(null);
    }
  };

  const handleEnable = async (job: Job) => {
    setActionLoading(job.id);
    try {
      await enableJob(job.id);
      await loadJobs();
    } catch (err: unknown) {
      if (err && typeof err === "object" && "message" in err) {
        alert(String(err.message));
      }
    } finally {
      setActionLoading(null);
    }
  };

  const handleRunNow = async (job: Job) => {
    setActionLoading(job.id);

    let idempotencyKey = runNowKeysRef.current.get(job.id);
    if (!idempotencyKey) {
      idempotencyKey = crypto.randomUUID();
      runNowKeysRef.current.set(job.id, idempotencyKey);
    }

    try {
      const execution = await runJob(job.id, idempotencyKey);
      runNowKeysRef.current.delete(job.id);
      router.push(`/executions/${execution.id}`);
    } catch (err: unknown) {
      if (err && typeof err === "object" && "message" in err) {
        alert(String(err.message));
      }
    } finally {
      if (!runNowKeysRef.current.has(job.id)) {
        setActionLoading(null);
      }
    }
  };

  const handleDisable = async (job: Job) => {
    setActionLoading(job.id);
    try {
      await disableJob(job.id);
      await loadJobs();
    } catch (err: unknown) {
      if (err && typeof err === "object" && "message" in err) {
        alert(String(err.message));
      }
    } finally {
      setActionLoading(null);
    }
  };

  return (
    <div className="mx-auto max-w-5xl px-4 py-10">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Jobs</h1>
          <p className="mt-1 text-slate-600">
            Create and manage automated HTTP jobs.
          </p>
        </div>
        {!showForm && !editingJob && (
          <button
            type="button"
            onClick={() => setShowForm(true)}
            className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700"
          >
            + Create Job
          </button>
        )}
      </div>

      {(showForm || editingJob) && (
        <div className="mt-8 rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <h2 className="text-lg font-semibold text-slate-900">
            {editingJob ? "Edit Job" : "New Job"}
          </h2>
          <div className="mt-4">
            <JobForm
              initial={editingJob ?? undefined}
              onSubmit={editingJob ? handleUpdate : handleCreate}
              onCancel={() => {
                setShowForm(false);
                setEditingJob(null);
              }}
            />
          </div>
        </div>
      )}

      <div className="mt-8 flex flex-wrap gap-4">
        <input
          type="search"
          placeholder="Search by name..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
        />
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
        >
          {STATUS_OPTIONS.map((opt) => (
            <option key={opt.value} value={opt.value}>
              {opt.label}
            </option>
          ))}
        </select>
      </div>

      {error && (
        <div className="mt-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      {loading ? (
        <div className="mt-8 text-center text-slate-500">Loading jobs...</div>
      ) : jobs.length === 0 ? (
        <div className="mt-8 rounded-xl border border-dashed border-slate-300 bg-white p-12 text-center">
          <p className="text-slate-500">No jobs found. Create your first job to get started.</p>
        </div>
      ) : (
        <div className="mt-8 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="min-w-full divide-y divide-slate-200">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-slate-500">
                  Name
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-slate-500">
                  Method
                </th>
                <th className="hidden px-4 py-3 text-left text-xs font-medium uppercase text-slate-500 sm:table-cell">
                  Schedule
                </th>
                <th className="hidden px-4 py-3 text-left text-xs font-medium uppercase text-slate-500 md:table-cell">
                  Next Run (UTC)
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase text-slate-500">
                  Status
                </th>
                <th className="px-4 py-3 text-right text-xs font-medium uppercase text-slate-500">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200">
              {jobs.map((job) => (
                <tr key={job.id}>
                  <td className="px-4 py-3 text-sm font-medium text-slate-900">
                    {job.name}
                  </td>
                  <td className="px-4 py-3 text-sm text-slate-600">
                    {job.httpMethod}
                  </td>
                  <td className="hidden px-4 py-3 font-mono text-xs text-slate-600 sm:table-cell">
                    {job.schedule ?? "—"}
                  </td>
                  <td className="hidden px-4 py-3 text-sm text-slate-600 md:table-cell">
                    {job.nextRunAt
                      ? new Date(job.nextRunAt).toISOString().replace("T", " ").slice(0, 16)
                      : job.schedule
                        ? "—"
                        : "Manual only"}
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge status={job.status} />
                  </td>
                  <td className="px-4 py-3 text-right text-sm">
                    <div className="flex justify-end gap-2">
                      {job.status === "Active" && (
                        <button
                          type="button"
                          disabled={actionLoading === job.id}
                          onClick={() => handleRunNow(job)}
                          className="font-medium text-brand-600 hover:text-brand-700 disabled:opacity-50"
                        >
                          {actionLoading === job.id ? "Running..." : "Run Now"}
                        </button>
                      )}
                      <Link
                        href={`/jobs/${job.id}/executions`}
                        className="text-slate-600 hover:text-slate-900"
                      >
                        History
                      </Link>
                      <button
                        type="button"
                        onClick={() => {
                          setShowForm(false);
                          setEditingJob(job);
                        }}
                        className="text-brand-600 hover:text-brand-700"
                      >
                        Edit
                      </button>
                      {job.status === "Active" && (
                        <button
                          type="button"
                          disabled={actionLoading === job.id}
                          onClick={() => handleDisable(job)}
                          className="text-slate-600 hover:text-slate-900"
                        >
                          Disable
                        </button>
                      )}
                      {job.status === "Paused" && (
                        <button
                          type="button"
                          disabled={actionLoading === job.id}
                          onClick={() => handleEnable(job)}
                          className="text-slate-600 hover:text-slate-900"
                        >
                          Enable
                        </button>
                      )}
                      {job.status !== "Archived" && (
                        <button
                          type="button"
                          disabled={actionLoading === job.id}
                          onClick={() => handleArchive(job)}
                          className="text-red-600 hover:text-red-700"
                        >
                          Archive
                        </button>
                      )}
                    </div>
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

function StatusBadge({ status }: { status: JobStatus }) {
  const colors: Record<JobStatus, string> = {
    Active: "bg-green-100 text-green-800",
    Paused: "bg-yellow-100 text-yellow-800",
    Draft: "bg-slate-100 text-slate-800",
    Archived: "bg-red-100 text-red-800",
  };

  return (
    <span
      className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${colors[status]}`}
    >
      {status}
    </span>
  );
}
