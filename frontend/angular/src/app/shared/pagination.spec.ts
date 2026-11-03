import { TestBed } from '@angular/core/testing';
import { Pagination } from './pagination';

function render(page: number, totalCount: number) {
  const fixture = TestBed.createComponent(Pagination);
  fixture.componentRef.setInput('page', page);
  fixture.componentRef.setInput('pageSize', 10);
  fixture.componentRef.setInput('totalCount', totalCount);
  fixture.detectChanges();
  const element = fixture.nativeElement as HTMLElement;
  return [...element.querySelectorAll('.pagination-list li')].map((li) => li.textContent?.trim());
}

describe('Pagination', () => {
  it('is hidden for a single page', () => {
    expect(render(1, 10)).toEqual([]);
  });

  it('shows first, last and neighbouring pages', () => {
    expect(render(5, 100)).toEqual(['1', '…', '4', '5', '6', '…', '10']);
    expect(render(1, 30)).toEqual(['1', '2', '3']);
  });
});
