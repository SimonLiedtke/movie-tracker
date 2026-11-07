import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import ErrorNotification from './ErrorNotification.vue';

describe('ErrorNotification', () => {
  it('shows nothing without an error', () => {
    expect(
      mount(ErrorNotification, { props: { error: null } })
        .find('.notification')
        .exists(),
    ).toBe(false);
  });

  it('shows the message and the details of an error', () => {
    const notification = mount(ErrorNotification, {
      props: {
        error: {
          status: 400,
          message: 'One or more validation errors occurred.',
          details: ['The title field is required.', 'Unknown genre IDs: 99999.'],
        },
      },
    });

    expect(notification.get('[role=alert]').text()).toContain('One or more validation errors');
    expect(notification.findAll('li').map((li) => li.text())).toEqual([
      '• The title field is required.',
      '• Unknown genre IDs: 99999.',
    ]);
  });

  it('has no list without details', () => {
    const notification = mount(ErrorNotification, {
      props: { error: { status: 500, message: 'Request failed (HTTP 500).', details: [] } },
    });

    expect(notification.find('ul').exists()).toBe(false);
  });

  it('can be dismissed', async () => {
    const notification = mount(ErrorNotification, {
      props: { error: { status: 500, message: 'Request failed (HTTP 500).', details: [] } },
    });

    await notification.get('button.delete').trigger('click');

    expect(notification.emitted('dismiss')).toHaveLength(1);
  });
});
