"use client";

import { ApiError, type CreateJobPayload, type UpdateJobPayload } from "@/lib/api";
import type { Job } from "@/types";
import { useState } from "react";

const HTTP_METHODS = ["GET", "POST", "PUT", "PATCH", "DELETE"];

interface JobFormProps {
  initial?: Job;
  onSubmit: (payload: CreateJobPayload | UpdateJobPayload) => Promise<void>;
  onCancel: () => void;
}

export function JobForm({ initial, onSubmit, onCancel }: JobFormProps) {
  const [name, setName] = useState(initial?.name ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [httpMethod, setHttpMethod] = useState(initial?.httpMethod ?? "GET");
  const [url, setUrl] = useState(initial?.url ?? "");
  const [body, setBody] = useState(initial?.body ?? "");
  const [schedule, setSchedule] = useState(initial?.schedule ?? "");
  const [timeoutSeconds, setTimeoutSeconds] = useState(
    initial?.timeoutSeconds ?? 30
  );
  const [headers, setHeaders] = useState<{ key: string; value: string }[]>(
    initial?.headers
      ? Object.entries(initial.headers).map(([key, value]) => ({ key, value }))
      : []
  );
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [submitting, setSubmitting] = useState(false);

  const addHeader = () => setHeaders([...headers, { key: "", value: "" }]);

  const removeHeader = (index: number) =>
    setHeaders(headers.filter((_, i) => i !== index));

  const updateHeader = (
    index: number,
    field: "key" | "value",
    value: string
  ) => {
    const next = [...headers];
    next[index] = { ...next[index], [field]: value };
    setHeaders(next);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setFieldErrors({});
    setSubmitting(true);

    const headerMap: Record<string, string> = {};
    for (const h of headers) {
      if (h.key.trim()) headerMap[h.key.trim()] = h.value;
    }

    const payload: CreateJobPayload = {
      name: name.trim(),
      description: description.trim() || undefined,
      url: url.trim(),
      httpMethod,
      headers: Object.keys(headerMap).length > 0 ? headerMap : undefined,
      body: body.trim() || undefined,
      schedule: schedule.trim() || undefined,
      timeoutSeconds,
    };

    try {
      await onSubmit(payload);
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        if (err.fields) setFieldErrors(err.fields);
        setError(err.message);
      } else if (err && typeof err === "object" && "message" in err) {
        setError(String(err.message));
      } else {
        setError("Failed to save job.");
      }
    } finally {
      setSubmitting(false);
    }
  };

  const fieldError = (field: string) => fieldErrors[field]?.[0];

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      <div>
        <label className="block text-sm font-medium text-slate-700">Name</label>
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          required
          className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
        />
        {fieldError("name") && (
          <p className="mt-1 text-xs text-red-600">{fieldError("name")}</p>
        )}
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-700">
          Description
        </label>
        <textarea
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          rows={2}
          className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
        />
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className="block text-sm font-medium text-slate-700">
            HTTP Method
          </label>
          <select
            value={httpMethod}
            onChange={(e) => setHttpMethod(e.target.value)}
            className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
          >
            {HTTP_METHODS.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </select>
          {fieldError("httpMethod") && (
            <p className="mt-1 text-xs text-red-600">
              {fieldError("httpMethod")}
            </p>
          )}
        </div>
        <div>
          <label className="block text-sm font-medium text-slate-700">
            Timeout (seconds)
          </label>
          <input
            type="number"
            min={1}
            max={300}
            value={timeoutSeconds}
            onChange={(e) => setTimeoutSeconds(Number(e.target.value))}
            className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
          />
          {fieldError("timeoutSeconds") && (
            <p className="mt-1 text-xs text-red-600">
              {fieldError("timeoutSeconds")}
            </p>
          )}
        </div>
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-700">URL</label>
        <input
          type="url"
          value={url}
          onChange={(e) => setUrl(e.target.value)}
          required
          placeholder="https://api.example.com/endpoint"
          className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
        />
        {fieldError("url") && (
          <p className="mt-1 text-xs text-red-600">{fieldError("url")}</p>
        )}
      </div>

      <div>
        <div className="flex items-center justify-between">
          <label className="block text-sm font-medium text-slate-700">
            Headers
          </label>
          <button
            type="button"
            onClick={addHeader}
            className="text-sm text-brand-600 hover:text-brand-700"
          >
            + Add Header
          </button>
        </div>
        <div className="mt-2 space-y-2">
          {headers.map((h, i) => (
            <div key={i} className="flex gap-2">
              <input
                value={h.key}
                onChange={(e) => updateHeader(i, "key", e.target.value)}
                placeholder="Header name"
                className="flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm"
              />
              <input
                value={h.value}
                onChange={(e) => updateHeader(i, "value", e.target.value)}
                placeholder="Value"
                className="flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm"
              />
              <button
                type="button"
                onClick={() => removeHeader(i)}
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-600 hover:bg-slate-50"
              >
                Remove
              </button>
            </div>
          ))}
        </div>
        {fieldError("headers") && (
          <p className="mt-1 text-xs text-red-600">{fieldError("headers")}</p>
        )}
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-700">Body</label>
        <textarea
          value={body}
          onChange={(e) => setBody(e.target.value)}
          rows={4}
          placeholder='{"key": "value"}'
          className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 font-mono text-sm"
        />
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-700">
          Schedule (optional, UTC cron)
        </label>
        <input
          value={schedule}
          onChange={(e) => setSchedule(e.target.value)}
          placeholder="0 9 * * *"
          className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 font-mono text-sm"
        />
        <p className="mt-1 text-xs text-slate-500">
          Schedules use UTC. Examples:{" "}
          <code className="rounded bg-slate-100 px-1">*/5 * * * *</code> every 5
          min · <code className="rounded bg-slate-100 px-1">0 * * * *</code> hourly ·{" "}
          <code className="rounded bg-slate-100 px-1">0 9 * * *</code> daily 09:00 UTC ·{" "}
          <code className="rounded bg-slate-100 px-1">0 9 * * 1-5</code> weekdays 09:00 UTC
        </p>
        {fieldError("schedule") && (
          <p className="mt-1 text-xs text-red-600">{fieldError("schedule")}</p>
        )}
      </div>

      <div className="flex gap-3 pt-2">
        <button
          type="submit"
          disabled={submitting}
          className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
        >
          {submitting ? "Saving..." : initial ? "Update Job" : "Create Job"}
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}
