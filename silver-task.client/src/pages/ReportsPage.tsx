import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useCurrentUser } from '@/hooks/useAuth';
import { getRecentReports, useRecentReports } from '@/hooks/useRecentReports';
import { ReportFilterBar } from '@/components/reports/ReportFilterBar';
import { ExportButtons } from '@/components/reports/ExportButtons';
import { TaskSummarySection } from '@/components/reports/TaskSummarySection';
import { OverdueSection } from '@/components/reports/OverdueSection';
import { ProjectProgressSection } from '@/components/reports/ProjectProgressSection';
import { WorkloadSection } from '@/components/reports/WorkloadSection';
import { TaskAgeSection } from '@/components/reports/TaskAgeSection';
import { CompletionTimeSection } from '@/components/reports/CompletionTimeSection';
import { CustomReportSection } from '@/components/reports/CustomReportSection';
import { MyReportsSection } from '@/components/reports/MyReportsSection';
import { AdminReportsSection } from '@/components/reports/AdminReportsSection';
import { DependencySection } from '@/components/reports/DependencySection';
import { TemplateUsageSection } from '@/components/reports/TemplateUsageSection';
import { CustomFieldSummarySection } from '@/components/reports/CustomFieldSummarySection';
import type { ReportConfiguration, ReportFilters, ReportGroupByField } from '@/types/reports';
import './ReportsPage.css';

type TabKey =
  | 'tasks'
  | 'overdue'
  | 'projects'
  | 'workload'
  | 'task-age'
  | 'completion-time'
  | 'dependencies'
  | 'custom'
  | 'templates'
  | 'custom-fields'
  | 'my'
  | 'admin';

const TABS: { key: TabKey; label: string; adminOnly?: boolean }[] = [
  { key: 'tasks', label: 'Task Summary' },
  { key: 'overdue', label: 'Overdue' },
  { key: 'projects', label: 'Projects' },
  { key: 'workload', label: 'Workload' },
  { key: 'task-age', label: 'Task Age' },
  { key: 'completion-time', label: 'Completion Time' },
  { key: 'dependencies', label: 'Dependencies' },
  { key: 'custom', label: 'Custom' },
  { key: 'templates', label: 'Templates' },
  { key: 'custom-fields', label: 'Custom Fields' },
  { key: 'my', label: 'My Reports' },
  { key: 'admin', label: 'Admin', adminOnly: true },
];

const TAB_REPORT_TYPE = {
  tasks: 'TaskSummary',
  overdue: 'Overdue',
  projects: 'ProjectProgress',
  workload: 'Workload',
  'task-age': 'TaskAge',
  'completion-time': 'CompletionTime',
  // The Dependencies tab shows several sub-reports at once (summary/blocked/bottlenecks/chain);
  // Blocked Tasks is the richest exportable table among them, so it's what the tab's export
  // buttons produce — see ReportsController.Export's BlockedTasks case.
  dependencies: 'BlockedTasks',
  custom: 'Custom',
} as const;

// The Phase 38 Reporting Center — a single page with internal tabs (matching the spec's own
// mockup: Task Summary/Completion/Overdue/Projects/Workload/Task Age/Completion Time buttons +
// Filters + Results), rather than a full nested-route layout per report type — kept manageable
// per the spec's own "do not build a full BI platform" instruction. The active tab is still a
// real, linkable URL segment (/reports/:type), satisfying the spec's own suggested report URLs.
export function ReportsPage() {
  const navigate = useNavigate();
  const { type } = useParams<{ type?: string }>();
  const { data: user } = useCurrentUser();
  const isAdmin = user?.role === 'Administrator';
  const activeTab: TabKey = (TABS.some((t) => t.key === type) ? type : 'tasks') as TabKey;

  const [filters, setFilters] = useState<ReportFilters>({ dateRange: 'thisMonth' });
  const [groupBy, setGroupBy] = useState<ReportGroupByField>('Project');

  const activeTabLabel = TABS.find((t) => t.key === activeTab)?.label ?? 'Task Summary';
  useRecentReports(activeTab, activeTabLabel);
  const recent = getRecentReports().filter((r) => r.path !== `/reports/${activeTab}`);

  function goToTab(tab: TabKey) {
    navigate(`/reports/${tab}`);
  }

  function openSavedReport(config: ReportConfiguration) {
    const { reportType, groupBy: savedGroupBy, ...rest } = config;
    setFilters(rest);
    if (savedGroupBy) setGroupBy(savedGroupBy);

    const tabForType = (Object.entries(TAB_REPORT_TYPE).find(([, rt]) => rt === reportType)?.[0] as TabKey | undefined) ?? 'tasks';
    goToTab(tabForType);
  }

  const exportReportType = activeTab in TAB_REPORT_TYPE ? TAB_REPORT_TYPE[activeTab as keyof typeof TAB_REPORT_TYPE] : null;

  return (
    <div className="reports-page">
      <div className="reports-page__header">
        <h1>Reports</h1>
        {exportReportType && (
          <ExportButtons
            reportType={exportReportType}
            filters={filters}
            extra={exportReportType === 'Custom' ? { groupBy } : undefined}
          />
        )}
      </div>

      <nav className="reports-page__tabs" role="tablist">
        {TABS.filter((t) => !t.adminOnly || isAdmin).map((tab) => (
          <button
            key={tab.key}
            type="button"
            role="tab"
            aria-selected={activeTab === tab.key}
            className={`reports-page__tab${activeTab === tab.key ? ' reports-page__tab--active' : ''}`}
            onClick={() => goToTab(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </nav>

      {recent.length > 0 && (
        <div className="reports-page__recent">
          <span>Recent:</span>
          {recent.map((r) => (
            <button key={r.path} type="button" onClick={() => navigate(r.path)}>
              {r.label}
            </button>
          ))}
        </div>
      )}

      {activeTab !== 'my' && activeTab !== 'templates' && activeTab !== 'custom-fields' && (
        <ReportFilterBar filters={filters} onChange={setFilters} />
      )}

      <div className="reports-page__content">
        {activeTab === 'tasks' && <TaskSummarySection filters={filters} />}
        {activeTab === 'overdue' && <OverdueSection filters={filters} onFiltersChange={setFilters} />}
        {activeTab === 'projects' && <ProjectProgressSection filters={filters} />}
        {activeTab === 'workload' && <WorkloadSection filters={filters} />}
        {activeTab === 'task-age' && <TaskAgeSection filters={filters} />}
        {activeTab === 'completion-time' && <CompletionTimeSection filters={filters} />}
        {activeTab === 'dependencies' && <DependencySection filters={filters} />}
        {activeTab === 'custom' && <CustomReportSection filters={filters} groupBy={groupBy} onGroupByChange={setGroupBy} />}
        {activeTab === 'templates' && <TemplateUsageSection />}
        {activeTab === 'custom-fields' && <CustomFieldSummarySection />}
        {activeTab === 'my' && (
          <MyReportsSection
            currentReportType={exportReportType ?? 'TaskSummary'}
            currentFilters={filters}
            currentGroupBy={groupBy}
            onOpenSavedReport={openSavedReport}
          />
        )}
        {activeTab === 'admin' && isAdmin && <AdminReportsSection filters={filters} />}
      </div>
    </div>
  );
}
