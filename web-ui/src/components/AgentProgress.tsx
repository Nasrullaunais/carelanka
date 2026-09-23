import type { BedAgentStep } from '../services/api/generated';

/**
 * What an agent has worked out so far, not a spinner with a caption bolted on. The plan comes
 * back the instant a run starts, and `steps` fills in as the run actually completes them, so
 * this reads the real state of the run rather than a guess timed to feel about right.
 *
 * Generic across agents: the caller supplies its own step captions, since each agent's plan
 * step names are its own (`gather_item`, `draft_recommendation`, ...).
 */
export function AgentProgress({
  plan,
  steps,
  captions,
}: {
  plan: string[] | null | undefined;
  steps: BedAgentStep[] | null | undefined;
  captions: Record<string, string>;
}) {
  const order = plan && plan.length > 0 ? plan : Object.keys(captions);
  const done = new Set((steps ?? []).filter((step) => step.ok !== false).map((step) => step.step));
  const currentIndex = order.findIndex((step) => !done.has(step));

  return (
    <ul className="agent-progress" style={{ listStyle: 'none', margin: '0.5rem 0 0', padding: 0 }}>
      {order.map((step, index) => {
        const isDone = done.has(step);
        const isCurrent = !isDone && index === currentIndex;
        const caption = captions[step] ?? step;

        return (
          <li
            key={step}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '0.5rem',
              padding: '0.2rem 0',
              opacity: isDone || isCurrent ? 1 : 0.45,
            }}
          >
            <span aria-hidden="true">{isDone ? '✓' : isCurrent ? '…' : '·'}</span>
            <span className={isCurrent ? '' : 'muted'}>
              {caption}
              {isCurrent ? '…' : ''}
            </span>
          </li>
        );
      })}
    </ul>
  );
}
