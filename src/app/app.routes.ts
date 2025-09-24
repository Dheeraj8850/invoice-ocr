import { Routes } from '@angular/router';
import { InvoiceOcrComponent } from './invoice-ocr/invoice-ocr.component';

export const routes: Routes = [
    { path: 'invoice', component: InvoiceOcrComponent },
    { path: '', redirectTo: 'invoice', pathMatch: 'full' }
];
