import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { BaseHttpService } from './base-http.service';
import { BlogPost } from '../pages/blog/blog.types';
import { blogPosts, getBlogPost } from '../pages/blog/blog.data';

export interface BlogPostSummary {
  slug: string;
  title: string;
  description: string;
  dateIso: string;
  author: string;
  tags: string[];
}

export interface BlogPostDetail extends BlogPostSummary {
  contentHtml: string;
  contentMarkdown: string;
}

@Injectable({
  providedIn: 'root',
})
export class BlogService extends BaseHttpService {
  /**
   * Fetch all published blog posts (summaries).
   * Falls back to static data on API error.
   */
  getPosts(): Observable<BlogPostSummary[]> {
    return this.get<BlogPostSummary[]>('api/blog').pipe(
      catchError(() => {
        // Fallback: return static blog data
        return of(
          blogPosts.map(p => ({
            slug: p.slug,
            title: p.title,
            description: p.description,
            dateIso: p.dateIso,
            author: p.author,
            tags: p.tags,
          }))
        );
      })
    );
  }

  /**
   * Fetch a single blog post by slug (includes full content).
   * Falls back to static data on API error.
   */
  getPost(slug: string): Observable<BlogPostDetail | null> {
    return this.get<BlogPostDetail>(`api/blog/${encodeURIComponent(slug)}`).pipe(
      catchError(() => {
        // Fallback: return static blog data
        const staticPost = getBlogPost(slug);
        if (!staticPost) {
          return of(null);
        }
        return of({
          slug: staticPost.slug,
          title: staticPost.title,
          description: staticPost.description,
          dateIso: staticPost.dateIso,
          author: staticPost.author,
          tags: staticPost.tags,
          contentHtml: staticPost.contentHtml,
          contentMarkdown: '',
        } as BlogPostDetail);
      })
    );
  }
}
