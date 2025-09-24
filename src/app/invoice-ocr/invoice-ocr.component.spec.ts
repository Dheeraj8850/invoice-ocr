import { ComponentFixture, TestBed } from '@angular/core/testing';

import { InvoiceOcrComponent } from './invoice-ocr.component';

describe('InvoiceOcrComponent', () => {
  let component: InvoiceOcrComponent;
  let fixture: ComponentFixture<InvoiceOcrComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InvoiceOcrComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(InvoiceOcrComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
