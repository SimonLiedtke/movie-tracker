import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import Poster from './Poster.vue';

describe('Poster', () => {
  it('shows the poster', () => {
    const image = mount(Poster, { props: { url: 'https://image/alien.jpg', title: 'Alien' } }).get(
      'img',
    );

    expect(image.attributes('src')).toBe('https://image/alien.jpg');
    expect(image.attributes('alt')).toBe('Poster of Alien');
  });

  it('shows a placeholder without a poster', () => {
    const poster = mount(Poster, { props: { url: null, title: 'Alien' } });

    expect(poster.find('img').exists()).toBe(false);
    expect(poster.get('.poster-placeholder').text()).toBe('🎬');
  });

  it('shows the placeholder if the image cannot be loaded, until there is another image', async () => {
    const poster = mount(Poster, { props: { url: 'https://image/broken.jpg', title: 'Alien' } });

    await poster.get('img').trigger('error');
    expect(poster.find('img').exists()).toBe(false);
    expect(poster.find('.poster-placeholder').exists()).toBe(true);

    await poster.setProps({ url: 'https://image/alien.jpg' });
    expect(poster.get('img').attributes('src')).toBe('https://image/alien.jpg');
  });
});
