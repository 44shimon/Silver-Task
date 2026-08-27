import { ArrowDown, ArrowUp, Settings2 } from 'lucide-react';
import type { DashboardLayout, DashboardWidgetId } from '@/types/dashboard';
import { DEFAULT_LAYOUT, WIDGET_DEFINITIONS } from './dashboardWidgets';
import './DashboardCustomizePanel.css';

interface DashboardCustomizePanelProps {
  layout: DashboardLayout;
  onChange: (layout: DashboardLayout) => void;
  isAdmin: boolean;
  managesAnyProject: boolean;
  can: (permission: string) => boolean;
}

// Simple checkbox-visibility + up/down reordering — deliberately not drag-and-drop, per the
// spec's own "do not introduce a complicated drag/drop framework if one is not already
// available" instruction (this app has no DnD library at all today).
export function DashboardCustomizePanel({ layout, onChange, isAdmin, managesAnyProject, can }: DashboardCustomizePanelProps) {
  const availableWidgets = WIDGET_DEFINITIONS.filter(
    (w) =>
      (!w.requiresAdmin || isAdmin) &&
      (!w.requiresManagesAnyProject || managesAnyProject) &&
      (!w.requiresPermission || can(w.requiresPermission)),
  );
  const orderedAvailable = layout.order.filter((id) => availableWidgets.some((w) => w.id === id));

  function toggleVisible(id: DashboardWidgetId) {
    const isVisible = layout.visibleWidgets.includes(id);
    onChange({
      ...layout,
      visibleWidgets: isVisible ? layout.visibleWidgets.filter((w) => w !== id) : [...layout.visibleWidgets, id],
    });
  }

  // Swaps against the adjacent *visible-to-this-user* widget, not the adjacent entry in the raw
  // order array — those can differ when a hidden admin/manager-only widget sits between them, in
  // which case swapping raw neighbors would silently do nothing the user can see.
  function move(id: DashboardWidgetId, direction: -1 | 1) {
    const visibleIndex = orderedAvailable.indexOf(id);
    const neighborId = orderedAvailable[visibleIndex + direction];
    if (!neighborId) {
      return;
    }
    const nextOrder = [...layout.order];
    const a = nextOrder.indexOf(id);
    const b = nextOrder.indexOf(neighborId);
    [nextOrder[a], nextOrder[b]] = [nextOrder[b], nextOrder[a]];
    onChange({ ...layout, order: nextOrder });
  }

  return (
    <details className="dashboard-customize-panel">
      <summary className="dashboard-customize-panel__trigger">
        <Settings2 size={14} />
        <span>Customize Dashboard</span>
      </summary>

      <div className="dashboard-customize-panel__body">
        <ul className="dashboard-customize-panel__list">
          {orderedAvailable.map((id, index) => {
            const definition = availableWidgets.find((w) => w.id === id)!;
            return (
              <li key={id} className="dashboard-customize-panel__row">
                <label>
                  <input type="checkbox" checked={layout.visibleWidgets.includes(id)} onChange={() => toggleVisible(id)} />
                  {definition.label}
                </label>
                <div className="dashboard-customize-panel__reorder">
                  <button type="button" aria-label={`Move ${definition.label} up`} disabled={index === 0} onClick={() => move(id, -1)}>
                    <ArrowUp size={12} />
                  </button>
                  <button
                    type="button"
                    aria-label={`Move ${definition.label} down`}
                    disabled={index === orderedAvailable.length - 1}
                    onClick={() => move(id, 1)}
                  >
                    <ArrowDown size={12} />
                  </button>
                </div>
              </li>
            );
          })}
        </ul>

        <button type="button" className="dashboard-customize-panel__reset" onClick={() => onChange(DEFAULT_LAYOUT)}>
          Reset Dashboard
        </button>
      </div>
    </details>
  );
}
