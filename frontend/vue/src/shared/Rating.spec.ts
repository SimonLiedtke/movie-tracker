import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import Rating from './Rating.vue';

describe('Rating', () => {
  it('shows the stars, and the rating as text for assistive technology', () => {
    const star = mount(Rating, { props: { halfStars: 7 } }).get('.rating-stars');

    expect(star.text()).toBe('★★★½');
    expect(star.attributes('title')).toBe('3.5 of 5 stars');
    expect(star.attributes('aria-label')).toBe('3.5 of 5 stars');
  });

  it('shows nothing without a rating', () => {
    expect(
      mount(Rating, { props: { halfStars: null } })
        .find('.rating-stars')
        .exists(),
    ).toBe(false);
    expect(mount(Rating).find('.rating-stars').exists()).toBe(false);
  });
});
