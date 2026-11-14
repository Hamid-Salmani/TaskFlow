import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { Modal } from './Modal';

function Harness({ onClose }: { onClose?: () => void }) {
  const [open, setOpen] = useState(false);
  return (
    <div>
      <button onClick={() => setOpen(true)}>Open dialog</button>
      <Modal
        open={open}
        onClose={() => {
          onClose?.();
          setOpen(false);
        }}
        title="Edit card"
        description="Change the card details"
      >
        <input aria-label="Title" />
        <button>Save</button>
      </Modal>
    </div>
  );
}

describe('Modal', () => {
  it('renders nothing while closed', () => {
    render(
      <Modal open={false} onClose={() => undefined} title="Hidden">
        content
      </Modal>,
    );

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('exposes an accessible dialog with title and description', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole('button', { name: 'Open dialog' }));

    const dialog = screen.getByRole('dialog', { name: 'Edit card' });
    expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(dialog).toHaveAccessibleDescription('Change the card details');
  });

  it('moves focus to the first control of the content when opened', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole('button', { name: 'Open dialog' }));

    expect(screen.getByLabelText('Title')).toHaveFocus();
  });

  it('closes on Escape', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<Harness onClose={onClose} />);
    await user.click(screen.getByRole('button', { name: 'Open dialog' }));

    await user.keyboard('{Escape}');

    expect(onClose).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('still closes on Escape when focus was lost to the page body', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<Harness onClose={onClose} />);
    await user.click(screen.getByRole('button', { name: 'Open dialog' }));
    (document.activeElement as HTMLElement | null)?.blur();
    expect(document.body).toHaveFocus();

    await user.keyboard('{Escape}');

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('pulls focus back into the dialog on Tab when it was lost', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByRole('button', { name: 'Open dialog' }));
    (document.activeElement as HTMLElement | null)?.blur();

    await user.tab();

    expect(screen.getByRole('dialog')).toContainElement(document.activeElement as HTMLElement);
  });

  it('returns focus to the element that opened it', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    const opener = screen.getByRole('button', { name: 'Open dialog' });
    await user.click(opener);

    await user.keyboard('{Escape}');

    expect(opener).toHaveFocus();
  });

  it('traps Tab inside the dialog in both directions', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByRole('button', { name: 'Open dialog' }));
    const title = screen.getByLabelText('Title');
    const save = screen.getByRole('button', { name: 'Save' });
    const close = screen.getByRole('button', { name: 'Close dialog' });

    // Order in the DOM: close, title, save.
    save.focus();
    await user.tab();
    expect(close).toHaveFocus();

    await user.tab({ shift: true });
    expect(save).toHaveFocus();

    title.focus();
    await user.tab();
    expect(save).toHaveFocus();
  });

  it('closes when the overlay is clicked but not when the panel is clicked', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<Harness onClose={onClose} />);
    await user.click(screen.getByRole('button', { name: 'Open dialog' }));

    await user.click(screen.getByRole('dialog'));
    expect(onClose).not.toHaveBeenCalled();

    const overlay = screen.getByRole('dialog').parentElement;
    expect(overlay).not.toBeNull();
    if (overlay) {
      await user.click(overlay);
    }
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('locks body scroll while open and restores it afterwards', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole('button', { name: 'Open dialog' }));
    expect(document.body.style.overflow).toBe('hidden');

    await user.keyboard('{Escape}');
    expect(document.body.style.overflow).toBe('');
  });
});
