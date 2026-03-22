import type { Routes } from '@angular/router';
import { SignupListComponent } from './signup-list.component';
import { SignupNewComponent } from './signup-new.component';
import { SignupEditComponent } from './signup-edit.component';
import { LoadPrepayComponent } from './load-prepay.component';

export const signupsRoutes: Routes = [
  { path: '', component: SignupListComponent },
  { path: 'new', component: SignupNewComponent },
  { path: 'edit/:id', component: SignupEditComponent },
  { path: 'prepay', component: LoadPrepayComponent },
];
