import type { ExecutionStatus } from "@/types";

const colors: Record<ExecutionStatus, string> = {
  Queued: "bg-slate-100 text-slate-800",
  Running: "bg-blue-100 text-blue-800",
  Succeeded: "bg-green-100 text-green-800",
  Failed: "bg-red-100 text-red-800",
  Retrying: "bg-yellow-100 text-yellow-800",
  Cancelled: "bg-slate-100 text-slate-500",
};

const labels: Partial<Record<ExecutionStatus, string>> = {
  Retrying: "Retrying…",
};

export function ExecutionStatusBadge({ status }: { status: ExecutionStatus }) {
  return (
    <span
      className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${colors[status]}`}
    >
      {labels[status] ?? status}
    </span>
  );
}
