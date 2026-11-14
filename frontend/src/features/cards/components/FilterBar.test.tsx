import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes, useLocation } from 'react-router-dom';
import type { LabelDto, WorkspaceMemberDto } from '@/shared/api/types';
import { renderWithProviders } from '@/test/utils';
import { useCardFilters } from '../hooks/useCardFilters';
import { FilterBar } from './FilterBar';

const labels: LabelDto[] = [{ id: 'l1', boardId: 'b', name: 'Bug', color: '#ef4444' }];
const members: WorkspaceMemberDto[] = [
  {
    userId: 'u1',
    email: 'a@t.dev',
    displayName: 'Ada',
    handle: 'ada',
    role: 'Member',
    joinedAt: '2026-01-01T00:00:00Z',
  },
];

function Harness() {
  const [filters, setFilters] = useCardFilters();
  return (
    <>
      <FilterBar filters={filters} onChange={setFilters} labels={labels} members={members} />
      <output aria-label="location">{useLocation().search}</output>
    </>
  );
}

function renderBar(route = '/board') {
  return renderWithProviders(
    <Routes>
      <Route path="/board" element={<Harness />} />
    </Routes>,
    { route },
  );
}

describe('FilterBar', () => {
  it('writes the search text to the URL after the user stops typing', async () => {
    const user = userEvent.setup();
    renderBar();

    await user.type(screen.getByRole('searchbox', { name: 'Search cards' }), 'login');

    await waitFor(() => expect(screen.getByLabelText('location')).toHaveTextContent('?q=login'));
  });

  it('applies the selects immediately and keeps other params', async () => {
    const user = userEvent.setup();
    renderBar('/board?card=c1');

    await user.selectOptions(screen.getByLabelText('Label'), 'l1');
    await user.selectOptions(screen.getByLabelText('Assignee'), 'u1');
    await user.selectOptions(screen.getByLabelText('Due date'), 'with');

    const location = screen.getByLabelText('location').textContent ?? '';
    const params = new URLSearchParams(location);
    expect(Object.fromEntries(params)).toEqual({
      card: 'c1',
      label: 'l1',
      assignee: 'u1',
      due: 'with',
    });
  });

  it('starts from the filters in the URL', () => {
    renderBar('/board?q=bug&label=l1&due=without');

    expect(screen.getByRole('searchbox', { name: 'Search cards' })).toHaveValue('bug');
    expect(screen.getByLabelText('Label')).toHaveValue('l1');
    expect(screen.getByLabelText('Due date')).toHaveValue('without');
  });

  it('clears everything with one button, which only shows while filtering', async () => {
    const user = userEvent.setup();
    renderBar('/board?q=bug&due=with');

    await user.click(screen.getByRole('button', { name: 'Clear filters' }));

    expect(screen.getByLabelText('location')).toBeEmptyDOMElement();
    expect(screen.getByRole('searchbox', { name: 'Search cards' })).toHaveValue('');
    expect(screen.queryByRole('button', { name: 'Clear filters' })).not.toBeInTheDocument();
  });
});
