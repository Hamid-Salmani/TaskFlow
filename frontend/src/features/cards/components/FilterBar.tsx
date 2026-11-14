import { Search, X } from 'lucide-react';
import { useEffect, useId, useState } from 'react';
import type { LabelDto, WorkspaceMemberDto } from '@/shared/api/types';
import { Button, Select } from '@/shared/ui';
import { hasActiveFilters, type CardFilters, type DueFilter } from '../filters';

const SEARCH_DEBOUNCE_MS = 300;

interface FilterBarProps {
  filters: CardFilters;
  onChange: (filters: CardFilters) => void;
  labels: LabelDto[];
  members: WorkspaceMemberDto[];
}

/** Filters that cost a request (text) are debounced; selects apply immediately. The URL is the source of truth. */
export function FilterBar({ filters, onChange, labels, members }: FilterBarProps) {
  const id = useId();
  const [text, setText] = useState(filters.q);

  // Follow the URL when it changes from outside (back button, "Clear filters").
  useEffect(() => setText(filters.q), [filters.q]);

  useEffect(() => {
    if (text === filters.q) {
      return;
    }
    const timer = setTimeout(() => onChange({ ...filters, q: text }), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [text, filters, onChange]);

  return (
    <form
      role="search"
      aria-label="Filter cards"
      className="flex flex-wrap items-end gap-2 px-4 pb-3"
      onSubmit={(event) => event.preventDefault()}
    >
      <div className="relative">
        <label htmlFor={`${id}-q`} className="sr-only">
          Search cards
        </label>
        <Search
          className="pointer-events-none absolute left-2.5 top-2.5 size-4 text-muted"
          aria-hidden="true"
        />
        <input
          id={`${id}-q`}
          type="search"
          value={text}
          onChange={(event) => setText(event.target.value)}
          placeholder="Search cards"
          className="h-9 w-48 rounded-md border border-border bg-surface pl-8 pr-2 text-sm placeholder:text-muted sm:w-56"
        />
      </div>

      <div>
        <label htmlFor={`${id}-label`} className="sr-only">
          Label
        </label>
        <Select
          id={`${id}-label`}
          className="h-9 w-36"
          value={filters.labelId ?? ''}
          onChange={(event) => onChange({ ...filters, labelId: event.target.value || null })}
        >
          <option value="">All labels</option>
          {labels.map((label) => (
            <option key={label.id} value={label.id}>
              {label.name}
            </option>
          ))}
        </Select>
      </div>

      <div>
        <label htmlFor={`${id}-assignee`} className="sr-only">
          Assignee
        </label>
        <Select
          id={`${id}-assignee`}
          className="h-9 w-40"
          value={filters.assigneeId ?? ''}
          onChange={(event) => onChange({ ...filters, assigneeId: event.target.value || null })}
        >
          <option value="">Anyone</option>
          {members.map((member) => (
            <option key={member.userId} value={member.userId}>
              {member.displayName}
            </option>
          ))}
        </Select>
      </div>

      <div>
        <label htmlFor={`${id}-due`} className="sr-only">
          Due date
        </label>
        <Select
          id={`${id}-due`}
          className="h-9 w-36"
          value={filters.due}
          onChange={(event) => {
            const value = event.target.value;
            const due: DueFilter = value === 'with' || value === 'without' ? value : 'any';
            onChange({ ...filters, due });
          }}
        >
          <option value="any">Any due date</option>
          <option value="with">Has due date</option>
          <option value="without">No due date</option>
        </Select>
      </div>

      {hasActiveFilters(filters) ? (
        <Button
          size="sm"
          variant="ghost"
          onClick={() => onChange({ q: '', labelId: null, assigneeId: null, due: 'any' })}
        >
          <X className="size-4" aria-hidden="true" />
          Clear filters
        </Button>
      ) : null}
    </form>
  );
}
