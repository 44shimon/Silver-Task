import type { TrendPoint } from '@/types/reports';
import './TrendChart.css';

interface TrendChartProps {
  points: TrendPoint[];
  /** What the y-axis represents, for the accessible summary and chart aria-label — e.g. "Tasks
   * completed" or "Percent complete". */
  label: string;
  valueSuffix?: string;
}

const WIDTH = 600;
const HEIGHT = 160;
const PADDING = 24;

// Hand-rolled SVG polyline chart (Phase 38) — deliberately not a charting library. This app has
// none anywhere (Kanban/Calendar/Timeline/Gantt are all styled-DOM/CSS with absolute-positioning
// math, see utils/timelineGrid.ts/calendarGrid.ts), and every trend this report suite needs is a
// single line over a handful of buckets — well within what ~50 lines of SVG can do, matching the
// spec's own "do not introduce a large charting library unnecessarily" instruction. A visually-
// hidden data table renders alongside the SVG so screen reader users get the same information
// without needing to interpret the chart visually (spec's own "charts must have accessible
// summaries" requirement) — the <table> is the source of truth, the SVG is a visual add-on.
export function TrendChart({ points, label, valueSuffix = '' }: TrendChartProps) {
  if (points.length === 0) {
    return <p className="trend-chart__empty">No data available for this range.</p>;
  }

  const values = points.map((p) => p.count);
  const max = Math.max(1, ...values);
  const min = Math.min(0, ...values);
  const range = max - min || 1;
  const stepX = points.length > 1 ? (WIDTH - PADDING * 2) / (points.length - 1) : 0;

  const coords = points.map((p, i) => ({
    x: points.length > 1 ? PADDING + i * stepX : WIDTH / 2,
    y: HEIGHT - PADDING - ((p.count - min) / range) * (HEIGHT - PADDING * 2),
    point: p,
  }));

  const polylinePoints = coords.map((c) => `${c.x.toFixed(1)},${c.y.toFixed(1)}`).join(' ');
  const first = points[0];
  const last = points[points.length - 1];

  return (
    <div className="trend-chart">
      <svg
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        className="trend-chart__svg"
        role="img"
        aria-label={`${label} trend from ${first.label} to ${last.label}, ranging from ${min}${valueSuffix} to ${max}${valueSuffix}. See the data table below for exact values.`}
      >
        <line x1={PADDING} y1={HEIGHT - PADDING} x2={WIDTH - PADDING} y2={HEIGHT - PADDING} className="trend-chart__axis" />
        <polyline points={polylinePoints} className="trend-chart__line" />
        {coords.map((c) => (
          <circle key={c.point.periodStart} cx={c.x} cy={c.y} r={3} className="trend-chart__point" />
        ))}
      </svg>

      <table className="trend-chart__data-table">
        <caption>{label} by period — accessible data table</caption>
        <thead>
          <tr>
            <th scope="col">Period</th>
            <th scope="col">{label}</th>
          </tr>
        </thead>
        <tbody>
          {points.map((p) => (
            <tr key={p.periodStart}>
              <td>{p.label}</td>
              <td>
                {p.count}
                {valueSuffix}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
