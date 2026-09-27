import { useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button, InputGroup, InputGroupInput, Label, Tab, TabIndicator, TabList, TabListContainer, Tabs, TextField } from '@heroui/react';
import type { EmergencyAgentPerformanceReport, FleetUtilisationReport, FleetUtilisationReportRow, ResponseTimeReport, ResponseTimeReportRow } from '../../../services/api/generated';
import { getEmergencyAgentPerformanceReportOptions, getEmergencyResponseTimeReportOptions, getFleetUtilisationReportOptions } from '../../../services/api/generated/@tanstack/react-query.gen';
import { DataTable, type DataTableColumn } from '../../../components/ui/data-table';
import { QueryState } from '../../../components/ui/query-state';
import { SummaryCard } from '../../../components/ui/summary-card';
import { priorityLabels } from '../domain';

interface DateRange { from: string; to: string }

export function EmergencyReports() {
  const initial = defaultRange();
  const [draft, setDraft] = useState<DateRange>(initial);
  const [range, setRange] = useState<DateRange>(initial);
  const [tab, setTab] = useState('response');
  const response = useQuery(getEmergencyResponseTimeReportOptions({ query: range }));
  const fleet = useQuery(getFleetUtilisationReportOptions({ query: range }));
  const agent = useQuery(getEmergencyAgentPerformanceReportOptions({ query: range }));

  function apply(event: FormEvent) {
    event.preventDefault();
    if (draft.from <= draft.to) setRange(draft);
  }

  return (
    <div className="flex flex-col gap-4">
      <section className="flex flex-col gap-3" aria-labelledby="emergency-reports-heading">
        <h2 id="emergency-reports-heading" className="m-0">Emergency reports</h2>
        <form className="flex flex-wrap items-end gap-3" onSubmit={apply}>
          <DateField label="From" value={draft.from} onChange={(from) => setDraft({ ...draft, from })} />
          <DateField label="To" value={draft.to} onChange={(to) => setDraft({ ...draft, to })} />
          <Button type="submit" isDisabled={draft.from === '' || draft.to === '' || draft.from > draft.to}>Apply range</Button>
        </form>
      </section>
      <QueryState query={response} errorContext="Could not load response-time reports." skeletonRows={4}>
        {(responseData) => <QueryState query={fleet} errorContext="Could not load fleet-utilisation reports." skeletonRows={4}>
          {(fleetData) => <QueryState query={agent} errorContext="Could not load agent-performance reports." skeletonRows={4}>
            {(agentData) => <ReportContent response={responseData} fleet={fleetData} agent={agentData} tab={tab} onTabChange={setTab} />}
          </QueryState>}
        </QueryState>}
      </QueryState>
    </div>
  );
}

function ReportContent({ response, fleet, agent, tab, onTabChange }: {
  response: ResponseTimeReport;
  fleet: FleetUtilisationReport;
  agent: EmergencyAgentPerformanceReport;
  tab: string;
  onTabChange: (tab: string) => void;
}) {
  const runs = (fleet.rows ?? []).reduce((total, row) => total + (row.run_count ?? 0), 0);
  return <>
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
      <SummaryCard title="Median decision time" value={minutes(response.totals?.median_minutes_to_dispatch)} hint="Call received to dispatch" />
      <SummaryCard title="Median drive time" value={minutes(response.totals?.median_minutes_to_arrival)} hint="Dispatch to scene arrival" />
      <SummaryCard title="Runs recorded" value={runs} hint="Dispatches overlapping the selected period" />
      <SummaryCard title="Agent agreement rate" value={percent(agent.confirmed_without_change_rate)} hint="Routine proposals confirmed unchanged" />
    </div>
    <Tabs selectedKey={tab} onSelectionChange={(key) => onTabChange(String(key))}>
      <TabListContainer><TabList>
        <Tab id="response"><TabIndicator />Response times</Tab>
        <Tab id="fleet"><TabIndicator />Fleet utilisation</Tab>
        <Tab id="agent"><TabIndicator />Agent performance</Tab>
      </TabList></TabListContainer>
    </Tabs>
    {tab === 'response' && <ResponseTimesTable rows={response.rows ?? []} />}
    {tab === 'fleet' && <FleetUtilisationTable rows={fleet.rows ?? []} />}
    {tab === 'agent' && <AgentPerformance report={agent} />}
  </>;
}

function DateField({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return <TextField value={value} onChange={onChange}><Label>{label}</Label><InputGroup><InputGroupInput type="date" required /></InputGroup></TextField>;
}

function ResponseTimesTable({ rows }: { rows: ResponseTimeReportRow[] }) {
  const columns: Array<DataTableColumn<ResponseTimeReportRow>> = [
    { key: 'priority', header: 'Priority', cell: (row) => priorityLabels[row.priority ?? 'high'] },
    { key: 'calls', header: 'Calls', cell: (row) => row.call_count ?? 0 },
    { key: 'decision', header: 'Median to dispatch', cell: (row) => minutes(row.median_minutes_to_dispatch) },
    { key: 'arrival', header: 'Median to arrival', cell: (row) => minutes(row.median_minutes_to_arrival) },
    { key: 'slowest', header: 'Slowest arrival', cell: (row) => minutes(row.slowest_minutes_to_arrival) },
  ];
  return <DataTable ariaLabel="Response times by priority" rows={rows} columns={columns} rowKey={(row) => row.priority ?? 'unknown'} rowText={(row) => priorityLabels[row.priority ?? 'high']} emptyMessage="No completed response times in this range." />;
}

function FleetUtilisationTable({ rows }: { rows: FleetUtilisationReportRow[] }) {
  const columns: Array<DataTableColumn<FleetUtilisationReportRow>> = [
    { key: 'ambulance', header: 'Ambulance', cell: (row) => row.registration_number ?? 'Unknown' },
    { key: 'runs', header: 'Runs', cell: (row) => row.run_count ?? 0 },
    { key: 'committed', header: 'Hours committed', cell: (row) => decimal(row.hours_committed) },
    { key: 'idle', header: 'Idle share', cell: (row) => percent(row.idle_share) },
    { key: 'out', header: 'Out-of-service hours', cell: (row) => decimal(row.out_of_service_hours) },
  ];
  return <DataTable ariaLabel="Fleet utilisation per vehicle" rows={rows} columns={columns} rowKey={(row) => row.ambulance_id ?? row.registration_number ?? 'unknown'} rowText={(row) => row.registration_number ?? 'Unknown ambulance'} emptyMessage="No fleet activity in this range." />;
}

function AgentPerformance({ report }: { report: EmergencyAgentPerformanceReport }) {
  const metrics = [
    ['Proposals raised', report.proposals_raised ?? 0],
    ['Confirmed', report.confirmed ?? 0],
    ['Diversions proposed', report.diversions_proposed ?? 0],
    ['Diversions approved', report.diversions_approved ?? 0],
    ['Diversions rejected', report.diversions_rejected ?? 0],
    ['No ambulance available', report.no_ambulance_available_count ?? 0],
    ['Validation failure rate', percent(report.validation_failure_rate)],
    ['Confirmed unchanged', percent(report.confirmed_without_change_rate)],
    ['Median proposal decision', `${decimal(report.median_seconds_proposal_to_confirm)} sec`],
    ['Median call to dispatch', minutes(report.median_minutes_call_to_dispatch)],
  ] as const;
  return <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">{metrics.map(([title, value]) => <SummaryCard key={title} title={title} value={value} />)}{Object.entries(report.rejection_reasons ?? {}).map(([reason, count]) => <SummaryCard key={reason} title={`Rejected: ${reason.replaceAll('_', ' ')}`} value={count} />)}</div>;
}

function defaultRange(): DateRange {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 30);
  return { from: dateInput(from), to: dateInput(to) };
}

function dateInput(date: Date): string {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
}

function minutes(value?: number): string { return value == null ? '—' : `${decimal(value)} min`; }
function decimal(value?: number): string { return value == null ? '—' : value.toFixed(1); }
function percent(value?: number): string { return value == null ? '—' : `${(value * 100).toFixed(1)}%`; }
