import Link from "next/link";

export default function HomePage() {
  return (
    <div className="mx-auto max-w-3xl px-4 py-16">
      <div className="rounded-2xl border border-slate-200 bg-white p-10 shadow-sm">
        <p className="text-sm font-medium uppercase tracking-wide text-brand-600">
          Job Automation Platform
        </p>
        <h1 className="mt-3 text-4xl font-bold text-slate-900">
          Automate HTTP jobs with confidence
        </h1>
        <p className="mt-4 text-lg text-slate-600">
          JobFlow lets you configure, schedule, and monitor API requests with
          full execution history, failure tracking, and retries.
        </p>
        <div className="mt-8 flex flex-wrap gap-3">
          <Link
            href="/register"
            className="rounded-lg bg-brand-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-brand-700"
          >
            Get started
          </Link>
          <Link
            href="/login"
            className="rounded-lg border border-slate-300 px-5 py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-50"
          >
            Sign in
          </Link>
          <Link
            href="/dashboard"
            className="rounded-lg border border-slate-300 px-5 py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-50"
          >
            View dashboard
          </Link>
        </div>
      </div>
    </div>
  );
}
