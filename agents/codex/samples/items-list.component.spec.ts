import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ItemsListComponent } from './items-list.component';
import { ItemsService } from './items.service';

describe('ItemsListComponent', () => {
  let fixture: ComponentFixture<ItemsListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ItemsListComponent, HttpClientTestingModule],
      providers: [ItemsService]
    }).compileComponents();

    fixture = TestBed.createComponent(ItemsListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('loads items and displays them', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne('/api/items?page=1&pageSize=20');
    req.flush([{ id: '1', title: 'First item', description: 'desc' }]);

    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelectorAll('li').length).toBe(1);
  });
});
