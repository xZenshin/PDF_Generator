import type { Bullet, Cv, Item, Section } from '../types'

/**
 * Mirrors CvPdfGenerator on the server: same inclusion rules, same ordering,
 * same "drop entries that would print blank". The PDF stays the source of truth,
 * this is the fast feedback loop.
 */
export function Preview({ cv }: { cv: Cv }) {
  const sections = cv.sections
    .filter((s) => s.included)
    .map((s) => ({ section: s, items: visibleItems(s) }))
    .filter((s) => s.items.length > 0)

  const contact = [cv.email, cv.phone, cv.location, cv.website].filter(Boolean).join(cv.style === 'Mono' ? ' | ' : ' · ')

  return (
    <div className={`paper paper-${cv.style.toLowerCase()}`}>
      <header className="paper-head">
        {cv.fullName && <h1>{cv.fullName}</h1>}
        {cv.headline && <p className="headline">{cv.headline}</p>}
        {contact && <p className="contact">{contact}</p>}
      </header>

      {cv.summary && <p className="summary">{cv.summary}</p>}

      {sections.map(({ section, items }) => (
        <section key={section.uid} className="paper-section">
          <h2>{section.title}</h2>
          {section.kind === 'Bullets' && section.twoColumns ? (
            <TwoColumnBullets bullets={items.flatMap(visibleBullets)} />
          ) : (
            items.map((item) => <PreviewItem key={item.uid} section={section} item={item} />)
          )}
        </section>
      ))}

      {sections.length === 0 && !cv.summary && (
        <p className="empty">Nothing is included yet — tick "In PDF" on the entries you want.</p>
      )}
    </div>
  )
}

/**
 * The section's bullets split the same way CvPdfGenerator splits them: left column
 * first, odd one out on the left. Two explicit lists rather than CSS `columns`, which
 * balances by height and would drift from the PDF whenever a bullet wraps.
 */
function TwoColumnBullets({ bullets }: { bullets: Bullet[] }) {
  const leftCount = Math.ceil(bullets.length / 2)

  return (
    <div className="bullet-columns">
      {[bullets.slice(0, leftCount), bullets.slice(leftCount)].map((column, index) => (
        <BulletList key={index} bullets={column} />
      ))}
    </div>
  )
}

/**
 * One bullet per row: a fixed-width glyph cell then the text, which is exactly what
 * ComposeBullets draws. A CSS list would put the marker in the padding and centre it
 * vertically, neither of which QuestPDF does.
 */
function BulletList({ bullets }: { bullets: Bullet[] }) {
  return (
    <div className="paper-bullets">
      {bullets.map((bullet) => (
        <div className="paper-bullet" key={bullet.uid}>
          <span className="dot">&bull;</span>
          <span>{bullet.text}</span>
        </div>
      ))}
    </div>
  )
}

function PreviewItem({ section, item }: { section: Section; item: Item }) {
  const bullets = visibleBullets(item)

  if (section.kind === 'Grouped') {
    return (
      <div className="grouped">
        <span className="grouped-label">{item.title}</span>
        <span>{bullets.map((b) => b.text).join(', ')}</span>
      </div>
    )
  }

  if (section.kind === 'FreeForm') {
    return (
      <div className="prose">
        {bullets.map((b) => (
          <p key={b.uid}>{b.text}</p>
        ))}
      </div>
    )
  }

  if (section.kind === 'Bullets') {
    return <BulletList bullets={bullets} />
  }

  const dates = [item.startDate, item.endDate].filter(Boolean).join(' – ')
  return (
    <div className="entry">
      <div className="entry-head">
        <div>
          {item.title && <strong>{item.title}</strong>}
          {item.organization && <div className="muted">{item.organization}</div>}
        </div>
        <div className="entry-meta">
          {dates && <div>{dates}</div>}
          {item.location && <div>{item.location}</div>}
        </div>
      </div>
      {bullets.length > 0 && <BulletList bullets={bullets} />}
    </div>
  )
}

const visibleBullets = (item: Item) => item.bullets.filter((b) => b.included && b.text.trim())

const visibleItems = (section: Section) =>
  section.items.filter((item) => {
    if (!item.included) return false
    if (section.kind === 'Timeline') {
      return Boolean(item.title.trim() || item.organization.trim() || visibleBullets(item).length)
    }
    return visibleBullets(item).length > 0
  })
