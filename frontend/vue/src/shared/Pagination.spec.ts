import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import Pagination from './Pagination.vue';

function render(page: number, totalCount: number) {
  return mount(Pagination, { props: { page, pageSize: 10, totalCount } });
}

const listed = (pagination: ReturnType<typeof render>) =>
  pagination.findAll('.pagination-list li').map((li) => li.text());

describe('Pagination', () => {
  it('is hidden for a single page', () => {
    expect(listed(render(1, 10))).toEqual([]);
    expect(render(1, 10).find('nav').exists()).toBe(false);
  });

  it('shows first, last and neighbouring pages', () => {
    expect(listed(render(5, 100))).toEqual(['1', '…', '4', '5', '6', '…', '10']);
    expect(listed(render(1, 30))).toEqual(['1', '2', '3']);
  });

  it('marks the current page', () => {
    const current = render(2, 30).get('.pagination-link.is-current');

    expect(current.text()).toBe('2');
    expect(current.attributes('aria-current')).toBe('page');
  });

  it('asks for the page that is clicked, the previous one or the next one', async () => {
    const pagination = render(2, 30);

    await pagination.get('.pagination-previous').trigger('click');
    await pagination.get('.pagination-next').trigger('click');
    await pagination.findAll('.pagination-link')[2].trigger('click');

    expect(pagination.emitted('pageChange')).toEqual([[1], [3], [3]]);
  });

  it('cannot go before the first or after the last page', () => {
    expect(render(1, 30).get('.pagination-previous').attributes('disabled')).toBeDefined();
    expect(render(1, 30).get('.pagination-next').attributes('disabled')).toBeUndefined();
    expect(render(3, 30).get('.pagination-next').attributes('disabled')).toBeDefined();
  });
});
