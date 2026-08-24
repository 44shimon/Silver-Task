import type { TimelineMonthBand, TimelineTick } from '@/utils/timelineGrid';

interface TimelineRulerProps {
  ticks: TimelineTick[];
  monthBands: TimelineMonthBand[];
}

// Shared by TimelineView and GanttView — the date header is identical in both; only what's
// rendered underneath it (a flat task list vs. a project-grouped one) differs.
export function TimelineRuler({ ticks, monthBands }: TimelineRulerProps) {
  return (
    <div className="timeline-view__ruler">
      {monthBands.map((band) => (
        <div key={band.key} className="timeline-view__month-band" style={{ left: band.left, width: band.width }}>
          <span>{band.label}</span>
        </div>
      ))}
      <div className="timeline-view__ticks">
        {ticks.map((tick) => (
          <div key={tick.key} className="timeline-view__tick" style={{ left: tick.left }}>
            <span>{tick.label}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
