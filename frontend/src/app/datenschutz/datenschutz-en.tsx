import {
  AUFSICHTSBEHOERDE,
  BETREIBER,
  HOSTING,
  NETZWERKDIENST,
  SICHERUNGSSPEICHER,
  SICHERUNG_AUFBEWAHRUNG,
  STAND_EN,
} from "@/lib/rechtliches";
import { Abschnitt, Liste } from "./datenschutz-bausteine";

/**
 * Englische Übersetzung der Datenschutzerklärung (page.tsx).
 *
 * Rechtlich verbindlich ist die deutsche Fassung; das sagt der Hinweis oben
 * auf der Seite (Fassungswahl). Gleiche Gliederung, gleiche Konstanten aus
 * rechtliches.ts - Anschrift, Hoster und Fristen stehen nirgends doppelt.
 *
 * Dieser Text steht bewusst NICHT in i18n/en.ts: Er ist ein zusammenhängender
 * Rechtstext und kein Baukasten aus Sätzen, die an mehreren Stellen vorkommen.
 * Deshalb steht app/datenschutz/ auf der Ausnahmeliste des Wächters
 * oberflaechen-luecken.test.ts.
 *
 * Ändert sich die deutsche Fassung, gehört die Änderung hierher - und STAND_EN
 * in rechtliches.ts auf das Datum von STAND. rechtliches.test.ts erinnert
 * daran und vergleicht die Zahl der Abschnitte.
 */
export function DatenschutzEn() {
  return (
    <>
      <section className="py-12 sm:py-16">
        <h1 className="text-3xl font-extrabold tracking-tight text-balance sm:text-4xl">Privacy Policy</h1>
        <p className="mt-4 text-base text-muted-foreground">
          Dogity is a training diary. To do its job, it stores what you enter – and nothing else. There is no advertising,
          no analytics services, no tracking and no resale of data. This page describes in detail what is stored, why,
          for how long and which rights you have.
        </p>
      </section>

      <Abschnitt titel="1. Who is responsible">
        <p className="text-foreground">
          {BETREIBER.name}
          <br />
          {BETREIBER.strasse}
          <br />
          {BETREIBER.plz} {BETREIBER.ort}
          <br />
          <a
            href={`mailto:${BETREIBER.email}`}
            className="text-primary-text underline-offset-4 hover:underline [overflow-wrap:anywhere]"
          >
            {BETREIBER.email}
          </a>
        </p>
        <p>
          No data protection officer has been appointed; the legal requirements for this are not met for a privately
          operated service of this size.
        </p>
      </Abschnitt>

      <Abschnitt titel="2. What data is stored">
        <p>Everything listed here arises because you enter it or use the app:</p>
        <p className="font-medium text-foreground">Account</p>
        <Liste
          punkte={[
            "First and last name, email address",
            "Password – never in plain text, only as a hash that cannot be reversed",
            "optionally, the web address of a profile picture",
            "Time of registration, role (member, trainer, admin)",
          ]}
        />
        <p className="font-medium text-foreground">Dogs</p>
        <Liste punkte={["Name, breed, date of birth, sex, notes and – if you upload one – a photo"]} />
        <p className="font-medium text-foreground">Training</p>
        <Liste
          punkte={[
            "Date, time and duration of the session",
            "Location as a name and – if you use your current position – as coordinates",
            "Weather data for the place and time of the session",
            "Exercises practised with your rating, success and notes",
            "Feedback from your trainers on individual exercises",
            "Your reply to a trainer’s feedback: “Thanks”, “Understood” or a short question",
            "Exam results (date, points, note) when you complete a goal",
          ]}
        />
        <p className="font-medium text-foreground">Tracks (location data)</p>
        <Liste
          punkte={[
            "the recorded track as a series of individual GPS points with timestamp and accuracy",
            "the dog’s path while running the track, likewise as a series of points",
            "evaluations calculated from this: deviation, articles found, hesitations",
          ]}
        />
        <p className="font-medium text-foreground">Club and groups</p>
        <Liste
          punkte={[
            "Memberships, join requests and invitations to a club including their status and the indication whether the request came through the club’s invitation link or QR code; for an open invitation, the email address the club entered for it",
            "Trainer roles, group membership and the assignment between trainer and dog",
            "planned group sessions that you take part in or lead",
            "your acceptance or decline of a group session including the time of your reply",
          ]}
        />
        <p className="font-medium text-foreground">Sign-up to a group (without an account)</p>
        <Liste
          punkte={[
            "Details that someone enters in a group’s sign-up form, and the trainers’ ticks for them – described in detail in section 9",
          ]}
        />
        <p className="font-medium text-foreground">Other</p>
        <Liste
          punkte={[
            "your learning progress in the theory-test question trainer (Sachkunde)",
            "Notifications within the app",
            "your settings: language, country, font size, hidden areas, chosen sports and whether important notifications are also sent by email",
            "Devices on which you have switched on push notifications: the device’s address at its manufacturer’s push service, the device’s keys for encryption, a short label such as “iPhone (Safari)” as well as the time of registration and of the last delivery – described in detail in section 7",
          ]}
        />
      </Abschnitt>

      <Abschnitt titel="3. Location data – the most sensitive part">
        <p>
          When recording a track, the app reads your device’s location. This produces a trace accurate to the metre of
          where you were and when – the most personal information Dogity stores. Therefore the following applies:
        </p>
        <Liste
          punkte={[
            "The recording only runs if you start it yourself, and only as long as it is visibly running.",
            "Your browser asks you for permission beforehand; you can withdraw it at any time.",
            "The points stay on Dogity’s server. They go to no map service and to no third party.",
            "The track is visible only to you, co-owners of the dog and trainers to whom the dog is assigned.",
            "If you delete the track or your account, the points disappear with it.",
            "If you share a track as an image, it is created on your device – without a map, without coordinates and without times – and Dogity transmits nothing in the process; where you pass the image on is up to you.",
          ]}
        />
        <p>
          When entering a training location, you can use your current position. This is a single point, not a recording
          – and voluntary; you can also type the place in by hand.
        </p>
      </Abschnitt>

      <Abschnitt titel="4. On what legal basis">
        <Liste
          punkte={[
            "Account, dogs, training sessions, tracks, club and settings: Art. 6(1)(b) GDPR – without this data the function you registered for does not exist.",
            "Sign-up to a group through a sign-up form (without an account): Art. 6(1)(b) GDPR – see section 9.",
            "Emails (welcome, password reset, invitations and decisions on join requests): Art. 6(1)(b) GDPR – they belong to the account and club functions. Whether you receive the emails about invitations and decisions, you choose at registration (preselected); you can unsubscribe at any time via the link in the email, without signing in, or under Profile → Features.",
            "Push notifications: Art. 6(1)(a) GDPR and Section 25(1) TDDDG (the German Telecommunications Digital Services Data Protection Act) – they only begin once you have switched them on yourself on a device and the browser or operating system has asked you for permission. You can switch them off in the app at any time or withdraw the permission in the device.",
            "Protection against abuse, troubleshooting and operational security: Art. 6(1)(f) GDPR. My legitimate interest is that the service stays available and accounts are not taken over.",
            "You expressly allow your browser to read your location and can withdraw this permission at any time.",
          ]}
        />
      </Abschnitt>

      <Abschnitt titel="5. Storage on your device – why there is no cookie banner">
        <p>
          Dogity itself sets no cookies. The app places a few values in your browser’s local storage, and only these:
        </p>
        <Liste
          punkte={[
            "your login token and the associated renewal token – without them you would be logged out on every page change",
            "your name, your email address and your role, so that the app does not first have to load them at startup",
            "the map type you last chose, the font size and which notice about news you have already seen",
            "if you have switched on push notifications: your device’s address at the push service, so that switching push off can remove it there again",
            "if you choose the language without an account (German or English): this choice, so that it is kept for your next visit – a pure convenience setting that is not sent to the server",
            "most recently loaded dogs, training sessions, goals, statistics and dog pictures as a cache, so that the app starts quickly and shows something even without a connection – it is deleted on logout and when another person logs in; for this, the app remembers the internal ID of the person who last logged in",
            "entries not yet sent, while you are offline – they go to the server as soon as there is a connection again, and only with your own login; if you log out beforehand, they stay on the device until your next login",
            "the abbreviation of an examination regulation, if you registered via its public page – only until you have created your first goal; it preselects the exam there, nothing more",
            "an ongoing GPS recording including locations, so that it survives a reload or crash of the app – it is deleted as soon as it has been saved or discarded",
          ]}
        />
        <p>
          All of this serves exclusively to operate the app you called up yourself, and is therefore permitted without
          consent under Section 25(2) TDDDG. None of it leaves your device towards third parties, and none of it serves
          to measure your behaviour. That is why there is nothing to accept or decline here – a banner would ask about
          something that does not happen. Logging out removes login, cache and ongoing recordings; entries not yet sent, map
          type, font size, news notice and language choice remain. Clearing the browser data removes everything.
        </p>
        <p>
          It is different with the upstream protection service Cloudflare (see section 6): To distinguish automated
          attacks from humans, it can insert a small check script into the page and set a short-lived security cookie.
          Both serve solely to protect the service and are likewise permitted without consent under Section 25(2) no. 2
          TDDDG.
        </p>
      </Abschnitt>

      <Abschnitt titel="6. Server, log files and backups">
        <p>
          Dogity runs on a rented server at {HOSTING.anbieter}. Location of the servers: {HOSTING.standortEn}. A data
          processing agreement under Art. 28 GDPR exists with the provider; it processes the data exclusively on
          instructions and for the operation.
        </p>
        <p>
          {NETZWERKDIENST.anbieter} sits in front of the server as a protection and relay service. Every request to
          Dogity first runs through Cloudflare’s network: There the encrypted connection to your device is established,
          attacks and mass automated access are repelled, and the request is forwarded, encrypted, to the server.
          Cloudflare processes your IP address and the other connection data in the process; because the encryption
          ends there and is set up anew, the content of the requests also runs through this network. The legal basis is
          Art. 6(1)(f) GDPR – my legitimate interest is a service that stays available and fends off attacks. A data
          processing agreement under Art. 28 GDPR exists with Cloudflare.
        </p>
        <p>
          Cloudflare is headquartered in the {NETZWERKDIENST.sitzEn}. Requests are usually handled in a data centre
          near you; processing in the USA cannot, however, be ruled out. Cloudflare is certified under the EU-US Data
          Privacy Framework, for which the European Commission has determined an adequate level of data protection
          (Art. 45 GDPR). In addition, Cloudflare can ask your browser to report failed connections directly to
          Cloudflare (Network Error Logging), and can convert email addresses on the page into an encrypted form that
          only your browser makes readable again – as protection against address harvesters.
        </p>
        <p>
          When the page is called up, technically necessary connection data arises – IP address, time, requested address,
          browser identifier. They serve operation and the defence against attacks (Art. 6(1)(f) GDPR) and are not
          merged with your account or used for evaluations. To limit mass guessing of passwords, the server also
          briefly counts login attempts per IP address.
        </p>
        <p>
          A backup of the database is created daily. It is encrypted before it leaves the server – in such a way that
          only the key for encrypting is on the server, not the one for decrypting. Anyone who gains access to the server
          can therefore not read the backups. They are stored on the server itself and additionally at{" "}
          {SICHERUNGSSPEICHER.anbieter} in storage that is fixed to the {SICHERUNGSSPEICHER.jurisdiktionEn}. A data
          processing agreement under Art. 28 GDPR also exists with this provider.
        </p>
        <p>
          Retention is staggered: {SICHERUNG_AUFBEWAHRUNG.lokal} days on the server, off-site{" "}
          {SICHERUNG_AUFBEWAHRUNG.taeglich} days for the daily versions, {SICHERUNG_AUFBEWAHRUNG.woechentlich} days for
          the weekly ones and {SICHERUNG_AUFBEWAHRUNG.monatlich} days for the monthly ones. If you delete something, it
          is gone from the running app immediately; from the backups it drops out with the respective version, in the
          extreme case after just over thirteen months. Backups are used exclusively for restoration after an
          outage.
        </p>
      </Abschnitt>

      <Abschnitt titel="7. Third-party services">
        <p>Dogity does not embed any analytics, advertising or social media services. These parties are involved:</p>
        <p className="font-medium text-foreground">Map (in your browser)</p>
        <p>
          Your device draws the map itself, from map data that it fetches directly from the{" "}
          <span className="text-foreground">OpenStreetMap Foundation</span> (United Kingdom). For technical reasons,
          the latter learns your IP address and the map section shown. Fonts and symbols of the map are delivered by
          Dogity itself; nothing goes to third parties for them. If you switch to the aerial image, the image tiles come
          from <span className="text-foreground">Esri</span>; data is transferred to the USA in the process. The aerial
          image is only loaded if you select it yourself – the map always starts with OpenStreetMap.
        </p>
        <p className="font-medium text-foreground">Place search and weather (from the server, not from your device)</p>
        <p>
          If you search for a training place, Dogity’s server queries the service{" "}
          <span className="text-foreground">Photon</span> (komoot GmbH, Berlin). For the temperature when laying and
          searching a track, it queries <span className="text-foreground">Open-Meteo</span>. In both cases only the
          request itself goes out – search term or coordinates and time respectively. Your IP address and your name are
          not transmitted; the services only see Dogity’s server.
        </p>
        <p className="font-medium text-foreground">Emails</p>
        <p>
          Once email sending is set up, Dogity sends emails only in these cases: a welcome email after registration, the
          link to reset your password on request, and – if you have not switched it off – invitations (to a club, to a
          group, as a trainer of a group, to co-ownership of a dog) and decisions on join requests. Everything else
          appears only in the app or as push. At registration you choose whether you receive these emails (the tick is
          preselected). Dogity remembers the language you set there (German or English): emails and push messages come
          in that language until you change it in your profile. The welcome email and each of these emails contain a
          link to unsubscribe that works without logging in; it contains only a signed identifier of your account, no
          details in plain text. The email for resetting the password has no such link, because you requested it
          yourself. Sending is done by a dedicated mail server on Dogity’s server at {HOSTING.anbieter} – without a
          sending service, without any further service provider. It passes the email directly to your email provider,
          encrypted if the latter supports it, and keeps it only until it is accepted – if your provider cannot be
          reached, at most two days. Its log contains recipient address, time and whether delivery worked, not the
          content; it is limited in size and continuously overwritten.
        </p>
        <p>
          If you write to {BETREIBER.email}, {NETZWERKDIENST.anbieter} accepts the email (Email Routing, see section 6)
          and forwards it to my personal mailbox, without storing it itself. I use your message and your address only to
          reply to you (Art. 6(1)(f) GDPR; if it concerns your account, Art. 6(1)(b) GDPR), and delete them when the matter is dealt
          with and no retention obligation exists.
        </p>
        <p className="font-medium text-foreground">Push services of the device manufacturers (from the server)</p>
        <p>
          If you switch on push notifications, the server sends the message to the push service that your browser or
          device specifies for this: <span className="text-foreground">Apple</span> (Safari, on the iPhone only for
          Dogity on the home screen), <span className="text-foreground">Google</span> (Chrome, Android and others),{" "}
          <span className="text-foreground">Mozilla</span> (Firefox) or{" "}
          <span className="text-foreground">Microsoft</span> (Edge on Windows). This service learns the address of your
          device, the time and size of the message – but not the content: it is end-to-end encrypted (RFC 8291), only
          your device can read it. Depending on the provider, processing may also take place in the USA. The services
          are controllers in their own right; they receive only what is necessary for delivery.
        </p>
        <p className="font-medium text-foreground">Ko-fi</p>
        <p>
          The page links to a donation page at Ko-fi. This is an ordinary link, not an embedded window and not a
          script: Nothing goes to Ko-fi unless you tap it yourself. What happens there is governed by Ko-fi’s terms.
        </p>
      </Abschnitt>

      <Abschnitt titel="8. Who else sees your data">
        <p>Within Dogity, other people see only what the collaboration requires:</p>
        <Liste
          punkte={[
            "Co-owners of a dog see its diary in full. Someone only becomes a co-owner by accepting an invitation. Until then the inviting person sees only the email address they entered themselves, and the invited person sees only the dog’s name and who is inviting. You can end your own co-ownership at any time.",
            "Trainers of a group that you have joined yourself or whose invitation you have accepted can look after your dogs. They then see their training sessions, goals and tracks and can give feedback. You are notified as soon as someone looks after one of your dogs; if you leave the group, the supervision ends. You can also end it yourself at any time: Under “Manage” on the dog you see which trainers have insight, and you end the supervision there with a tap. The trainer is notified and no longer sees the dog afterwards.",
            "Acceptances of group sessions: The trainers of the club and of the session see by name who accepted or declined. The other members of your group only see numbers, such as “5 acceptances”. If you decline, the trainers of the session receive a notification with your first name.",
            "Your reply to a trainer’s feedback (“Thanks”, “Understood”, “Question”) is seen by the trainer who gave it and by the owners of the dog; it triggers a notification for the trainer. If the trainer writes new feedback, your reply is reset.",
            "The club administration sees memberships, group membership and names of its members – not their training diary. For a join request it also sees whether it came through the club’s invitation link.",
            "In the administration, the operator sees aggregated key figures, such as how many of the accounts of the last 30 days have created a dog. These are numbers, not persons; nothing is measured in the browser for this.",
            "Anyone who invites you to a club, to a group, as a trainer of a group or as a co-owner of a dog sees, until you accept, only the email address entered for that – not your name. If you decline, it stays that way.",
            "For technical reasons the operator can access the database, but does so only for troubleshooting and operation.",
          ]}
        />
        <p>
          No disclosure to third parties for advertising or evaluation purposes takes place. Nor does any sale.
          Disclosure to authorities only insofar as I am legally obliged to.
        </p>
      </Abschnitt>

      <Abschnitt titel="9. Sign-up to a group through a sign-up form">
        <p>
          Groups of a club – for example a puppy group – can offer a sign-up form with a link and QR code. Anyone who
          signs up this way does <span className="text-foreground">not need a Dogity account</span> and does not become
          one. The following applies to these sign-ups:
        </p>
        <p className="font-medium text-foreground">What data is stored</p>
        <Liste
          punkte={[
            "First and last name, call name and breed of the dog, the dog’s date of birth (“Wurftag”) and phone number – what you enter in the form",
            "the time of the sign-up",
            "what the trainers note about it: on which days you were there with your dog, whether payment has been made, and – if they create one – a note",
            "for sign-ups that a club has taken over from an earlier form, the timestamp from back then",
          ]}
        />
        <p className="font-medium text-foreground">Purpose</p>
        <p>
          Solely for the club’s organisation of the group: so that the trainers know who has signed up, can call you if
          needed, record attendance and see who has already paid. The details are not used for other purposes, not
          passed on and not sold.
        </p>
        <p className="font-medium text-foreground">Who sees them</p>
        <p>
          The trainers of the group and the trainers of the club to which the group belongs – nobody else. Other members
          of the group, other people who signed up and visitors to the page also do not see the details. Anyone who
          opens the form sees only the name of the club and of the group. When there is a new sign-up, the trainers of
          the group receive a notification in the app that contains the dog’s call name and breed.
        </p>
        <p className="font-medium text-foreground">Legal basis</p>
        <p>
          Art. 6(1)(b) GDPR: The details are necessary to carry out your sign-up to the group. The tick in the form
          makes this visible; without the details a sign-up is not possible.
        </p>
        <p className="font-medium text-foreground">Deletion</p>
        <Liste
          punkte={[
            "The trainers can delete a sign-up at any time. The attendance ticks disappear with it, permanently.",
            "Sign-ups in which nothing has happened for twelve months – no attendance, no change, nothing marked as paid – are automatically deleted permanently. This is checked daily.",
            "If the group is dissolved, its sign-ups are deleted with it.",
            "How long it takes until a deletion also reaches the database backups is described in section 6.",
          ]}
        />
        <p>
          You can demand access, rectification or deletion from the club with which you signed up, or by message to{" "}
          <a
            href={`mailto:${BETREIBER.email}`}
            className="text-primary-text underline-offset-4 hover:underline [overflow-wrap:anywhere]"
          >
            {BETREIBER.email}
          </a>
          . The other rights from section 11 apply to you in the same way.
        </p>
      </Abschnitt>

      <Abschnitt titel="10. How long data is stored">
        <Liste
          punkte={[
            "Account data and everything attached to it: until you delete the account.",
            "Individual entries – training sessions, tracks, dogs: until you delete them.",
            "Sign-ups to a group without an account (section 9): until the trainers delete them, at the latest twelve months after the last attendance, change or sign-up.",
            "Login tokens on the server: the short-lived one after one hour, the renewal token after 60 days at the latest.",
            "Devices for push notifications: until you switch off push there, log out or delete your account; a device that the push service no longer knows is removed on the next send.",
            "Emails from Dogity: The mail server keeps them only until your email provider has accepted them, at most two days; its log (recipient, time, delivery) is limited in size and continuously overwritten (section 7).",
            `Emails to ${BETREIBER.email}: until the matter is dealt with, unless a retention obligation exists.`,
            `Database backups: ${SICHERUNG_AUFBEWAHRUNG.lokal} days on the server; off-site ${SICHERUNG_AUFBEWAHRUNG.taeglich} days (daily versions), ${SICHERUNG_AUFBEWAHRUNG.woechentlich} days (weekly) and ${SICHERUNG_AUFBEWAHRUNG.monatlich} days (monthly).`,
          ]}
        />
        <p>
          If you delete your account, your access, your dogs, training sessions, tracks, goals, settings, your learning
          progress, your acceptances of group sessions, the devices for push notifications and your club and group
          memberships are removed. Dogs that you share with someone remain with the other person – only your link to
          them disappears. Where you, as a trainer, have created content for a club, this content remains with the
          club, but the indication of your authorship is removed. Sign-ups to groups that you managed as a trainer
          belong to the club and remain; only the note that you set “paid” or ticked someone off is removed.
        </p>
      </Abschnitt>

      <Abschnitt titel="11. Your rights">
        <p>Under the GDPR you are entitled to:</p>
        <Liste
          punkte={[
            "Access to the data stored about you (Art. 15)",
            "Rectification of incorrect data (Art. 16)",
            "Erasure (Art. 17)",
            "Restriction of processing (Art. 18)",
            "Receiving your data in a portable format (Art. 20)",
            "Objection to processing that is based on legitimate interest (Art. 21)",
          ]}
        />
        <p className="text-foreground">
          For access and erasure you do not have to write to anyone: Under Profile → “Data & account” you find
          “Download my data” – this gives you everything stored as a file – and “Delete account”, which removes your
          account together with its data immediately.
        </p>
        <p>
          For everything else, a message to{" "}
          <a
            href={`mailto:${BETREIBER.email}`}
            className="text-primary-text underline-offset-4 hover:underline [overflow-wrap:anywhere]"
          >
            {BETREIBER.email}
          </a>{" "}
          is sufficient.
        </p>
        <p>
          Independently of this, you can lodge a complaint with a data protection supervisory authority (Art. 77 GDPR).
          The supervisory authority responsible for me (the controller) is:
        </p>
        <p className="text-foreground">
          {AUFSICHTSBEHOERDE.name}
          <br />
          ({AUFSICHTSBEHOERDE.nameEn})
          <br />
          {AUFSICHTSBEHOERDE.strasse}
          <br />
          {AUFSICHTSBEHOERDE.plz} {AUFSICHTSBEHOERDE.ort}
        </p>
      </Abschnitt>

      <Abschnitt titel="12. Security">
        <p>
          The connection is encrypted with TLS throughout, also between Cloudflare and the server. Passwords are stored
          only as a hash. Logins run via short-lived tokens that can be revoked on the server side; if a token that has
          already been used is presented again, all sessions of the account are ended, and anyone who changes or resets
          their password thereby logs out all other devices. After five wrong passwords in a row, the account is locked
          for a few minutes. The browser may load content only from defined sources (Content Security Policy), and login
          attempts are limited per IP address.
        </p>
      </Abschnitt>

      <Abschnitt titel="13. Automated decisions">
        <p>
          No automated decision with legal effect takes place. However, Dogity creates a training suggestion from your
          own ratings: Exercises that you rate lower are planned more often. This is a suggestion that you can change
          or ignore at any time.
        </p>
      </Abschnitt>

      <Abschnitt titel="14. Children and young people">
        <p>
          Dogity is aimed at adults. Anyone younger than 16 years should only create an account with the consent of
          their parents. If I learn that an account exists without this consent, I delete it.
        </p>
      </Abschnitt>

      <Abschnitt titel="15. Changes">
        <p>
          If a function is added that processes data differently, this text changes with it. The page{" "}
          <a href="/neuerungen" className="text-primary-text underline-offset-4 hover:underline">
            Changes (Neuerungen)
          </a>{" "}
          informs about significant changes.
        </p>
      </Abschnitt>

      <p className="border-t border-border/60 py-8 text-xs text-muted-foreground">Last updated: {STAND_EN}</p>
    </>
  );
}
