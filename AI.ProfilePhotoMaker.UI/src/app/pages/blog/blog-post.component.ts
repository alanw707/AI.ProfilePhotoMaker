import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MarketingFooterComponent } from '../../shared/marketing-footer/marketing-footer.component';
import { MarketingHeaderComponent } from '../../shared/marketing-header/marketing-header.component';
import { BlogPost } from './blog.types';
import { getBlogPost } from './blog.data';

@Component({
  selector: 'app-blog-post',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingHeaderComponent, MarketingFooterComponent],
  template: `
    <app-marketing-header></app-marketing-header>

    <main class="blog-page" *ngIf="post as p; else notFound">
      <article class="blog-post">
        <a routerLink="/blog" class="back-link">← Back to blog</a>
        <time [attr.datetime]="p.dateIso">{{ p.dateIso | date:'longDate' }}</time>
        <h1>{{ p.title }}</h1>
        <p class="post-description">{{ p.description }}</p>
        <div class="tags">
          <span class="tag" *ngFor="let tag of p.tags">{{ tag }}</span>
        </div>
        <div class="post-content" [innerHTML]="p.contentHtml"></div>
      </article>
    </main>

    <ng-template #notFound>
      <main class="blog-page">
        <article class="blog-post">
          <h1>Post not found</h1>
          <a routerLink="/blog" class="back-link">Back to blog</a>
        </article>
      </main>
    </ng-template>

    <app-marketing-footer></app-marketing-footer>
  `,
  styleUrls: ['./blog.sass'],
})
export class BlogPostComponent implements OnInit {
  post?: BlogPost;

  private readonly _route = inject(ActivatedRoute);
  private readonly _meta = inject(Meta);
  private readonly _title = inject(Title);

  ngOnInit(): void {
    const slug = this._route.snapshot.paramMap.get('slug') ?? '';
    this.post = getBlogPost(slug);

    if (this.post) {
      this._title.setTitle(`${this.post.title} - AI Profile Photo Maker`);
      this._meta.updateTag({ name: 'description', content: this.post.description });
    } else {
      this._title.setTitle('Blog Post Not Found - AI Profile Photo Maker');
      this._meta.updateTag({
        name: 'description',
        content: 'The requested blog post could not be found.',
      });
    }
  }
}
