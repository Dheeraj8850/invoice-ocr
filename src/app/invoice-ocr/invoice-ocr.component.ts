import { Component } from '@angular/core';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-invoice-ocr',
  standalone: true,
  imports: [CommonModule, HttpClientModule],
  templateUrl: './invoice-ocr.component.html',
  styleUrls: ['./invoice-ocr.component.css']
})
export class InvoiceOcrComponent {
  parsedJson: any = null;
  loading = false;
  errorMsg = '';
  imagePreview: string | null = null;

  constructor(private http: HttpClient) {}

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files && input.files[0];
    if (!file) return;

    this.errorMsg = '';
    this.parsedJson = null;
    this.loading = true;

    // (Optional) show a local preview
    const reader = new FileReader();
    reader.onload = () => (this.imagePreview = reader.result as string);
    reader.readAsDataURL(file);

    const formData = new FormData();
    formData.append('image', file);

    this.http.post('https://localhost:7158/api/OcrProcess/image-to-json', formData)
      .subscribe({
        next: (res) => {
          this.parsedJson = res;
          if (this.parsedJson?.lineItems){
            // this.fillMissingValues(this.parsedJson.lineItems,"size")
            this.fillMissingValues(this.parsedJson.lineItems, [
              "particulars",
              "colour",
              "size",
              "quantity",
              "unitPrice",
              "amount"
            ]);
          }
          this.loading = false;
        },
        error: (err) => {
          console.error('API Error:', err);
          this.errorMsg = (err?.error?.error) || 'Failed to process image.';
          this.loading = false;
        }
      });
  }
  // fillMissingValues(items:any[], field:string){
  //   let lastValue: any = null;
  //   for(let item of items){
  //     if(item[field] && item[field].trim() !== ""){
  //       lastValue = item[field]
  //     }else if(lastValue){
  //       item[field] = lastValue;
  //     }
  //   }
  // }
  fillMissingValues(items:any[], fields:string[]){
    let lastValues :{[key:string]:any}={};
    for(let item of items){
      for(let field of fields){
        if(item[field] && item[field].toString().trim() !== ""){
          lastValues[field]=item[field];
        }else if (lastValues[field]){
          item[field] = lastValues[field];
        }
      }
    }
  }
}
