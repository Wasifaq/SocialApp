import { inject, Injectable } from '@angular/core';
import { AccountService } from './account-service';
import { tap } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class InitService {
  private accountService = inject(AccountService);

  init() {
    // const userString = localStorage.getItem('user');
    // if (!userString) return of(null);
    // const user = JSON.parse(userString);
    return this.accountService.refreshToken().pipe(
      tap(user => {
        if(user) {
          this.accountService.setCurrentUser(user);
          this.accountService.startTokenRefreshInterval();
        }
      })
    )
  }
}
