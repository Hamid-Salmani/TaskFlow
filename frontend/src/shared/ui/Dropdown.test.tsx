import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Dropdown, type DropdownItem } from './Dropdown';
import { Modal } from './Modal';

function makeItems(onSelect = vi.fn()): DropdownItem[] {
  return [
    { id: 'profile', label: 'Profile', onSelect: () => onSelect('profile') },
    { id: 'settings', label: 'Settings', onSelect: () => onSelect('settings'), disabled: true },
    { id: 'billing', label: 'Billing', onSelect: () => onSelect('billing') },
    { id: 'logout', label: 'Sign out', onSelect: () => onSelect('logout'), destructive: true },
  ];
}

function renderDropdown(items = makeItems()) {
  return render(<Dropdown triggerLabel="Account menu" trigger={<span>Menu</span>} items={items} />);
}

describe('Dropdown', () => {
  it('is closed by default and announces its state', () => {
    renderDropdown();

    const trigger = screen.getByRole('button', { name: 'Account menu' });
    expect(trigger).toHaveAttribute('aria-haspopup', 'menu');
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('opens on click and focuses the first enabled item', async () => {
    const user = userEvent.setup();
    renderDropdown();

    await user.click(screen.getByRole('button', { name: 'Account menu' }));

    expect(screen.getByRole('menu')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Account menu' })).toHaveAttribute(
      'aria-expanded',
      'true',
    );
    expect(screen.getByRole('menuitem', { name: 'Profile' })).toHaveFocus();
  });

  it('opens with ArrowDown from the trigger', async () => {
    const user = userEvent.setup();
    renderDropdown();
    screen.getByRole('button', { name: 'Account menu' }).focus();

    await user.keyboard('{ArrowDown}');

    expect(screen.getByRole('menu')).toBeInTheDocument();
  });

  it('moves focus with ArrowDown/ArrowUp, wraps around and skips disabled items', async () => {
    const user = userEvent.setup();
    renderDropdown();
    await user.click(screen.getByRole('button', { name: 'Account menu' }));

    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('menuitem', { name: 'Billing' })).toHaveFocus();

    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('menuitem', { name: 'Sign out' })).toHaveFocus();

    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('menuitem', { name: 'Profile' })).toHaveFocus();

    await user.keyboard('{ArrowUp}');
    expect(screen.getByRole('menuitem', { name: 'Sign out' })).toHaveFocus();
  });

  it('jumps to the first and last item with Home and End', async () => {
    const user = userEvent.setup();
    renderDropdown();
    await user.click(screen.getByRole('button', { name: 'Account menu' }));

    await user.keyboard('{End}');
    expect(screen.getByRole('menuitem', { name: 'Sign out' })).toHaveFocus();

    await user.keyboard('{Home}');
    expect(screen.getByRole('menuitem', { name: 'Profile' })).toHaveFocus();
  });

  it('uses a roving tabindex so only one item is tabbable', async () => {
    const user = userEvent.setup();
    renderDropdown();
    await user.click(screen.getByRole('button', { name: 'Account menu' }));
    await user.keyboard('{ArrowDown}');

    const tabbable = screen.getAllByRole('menuitem').filter((item) => item.tabIndex === 0);
    expect(tabbable).toHaveLength(1);
    expect(tabbable[0]).toHaveTextContent('Billing');
  });

  it('selects with Enter, closes and returns focus to the trigger', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    renderDropdown(makeItems(onSelect));
    await user.click(screen.getByRole('button', { name: 'Account menu' }));
    await user.keyboard('{ArrowDown}{Enter}');

    expect(onSelect).toHaveBeenCalledWith('billing');
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Account menu' })).toHaveFocus();
  });

  it('does not activate disabled items', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    renderDropdown(makeItems(onSelect));
    await user.click(screen.getByRole('button', { name: 'Account menu' }));

    await user.click(screen.getByRole('menuitem', { name: 'Settings' }));

    expect(onSelect).not.toHaveBeenCalled();
  });

  it('closes on Escape and returns focus to the trigger', async () => {
    const user = userEvent.setup();
    renderDropdown();
    await user.click(screen.getByRole('button', { name: 'Account menu' }));

    await user.keyboard('{Escape}');

    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Account menu' })).toHaveFocus();
  });

  it('closes when clicking outside', async () => {
    const user = userEvent.setup();
    render(
      <div>
        <button>Outside</button>
        <Dropdown triggerLabel="Account menu" trigger="Menu" items={makeItems()} />
      </div>,
    );
    await user.click(screen.getByRole('button', { name: 'Account menu' }));

    await user.click(screen.getByRole('button', { name: 'Outside' }));

    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('shows an empty message when there are no items', async () => {
    const user = userEvent.setup();
    render(
      <Dropdown triggerLabel="Alerts" trigger="Alerts" items={[]} emptyMessage="All caught up" />,
    );

    await user.click(screen.getByRole('button', { name: 'Alerts' }));

    expect(screen.getByText('All caught up')).toBeInTheDocument();
  });

  it('closes only itself (not the surrounding modal) when Escape is pressed inside a modal', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(
      <Modal open onClose={onClose} title="Card">
        <Dropdown triggerLabel="Actions" trigger="Actions" items={makeItems()} />
      </Modal>,
    );
    await user.click(screen.getByRole('button', { name: 'Actions' }));

    await user.keyboard('{Escape}');

    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });
});
