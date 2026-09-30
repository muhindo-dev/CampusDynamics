-- ---------------------------------------------------------------------------
-- The eduroam Wi-Fi guide, in two places.
--
--   1. A portal notice for staff AND students, running in the marquee for three
--      months.
--   2. The video in the knowledge base, so it is still findable after the
--      marquee has expired. A notice scrolls past once; a knowledge base article
--      is what somebody searches for in February.
--
-- The notice text is reproduced exactly as MIS wrote it, including the sign off.
-- It is an official communication and not mine to reword.
-- ---------------------------------------------------------------------------

-- ── 1. The portal notice ───────────────────────────────────────────────────
--    target_audience BOTH is what puts it in front of staff as well as students;
--    the marquee reads that column directly. marquee_expiry is computed rather
--    than typed so the three months run from whenever this is applied.
INSERT INTO sys_communications
    (title, content, target_audience, is_force_read, force_read_expiry, priority,
     status, allow_comments, created_by, created_by_name, created_at, updated_at,
     published_at, show_in_marquee, marquee_expiry)
SELECT
    'How to Connect to eduroam Wi-Fi 📌',
    CONCAT(
      '<p>Dear Students,</p>',
      '<p><strong>eduroam</strong> is the University&rsquo;s secure Wi-Fi. You connect using your ',
      'University email and password, and your device connects automatically whenever you are ',
      'on campus.</p>',
      '<p><em>Before you start: be within range of eduroam and turn on your mobile data ',
      '(first time only).</em></p>',
      '<p><strong>Steps:</strong></p>',
      '<ul>',
        '<li>Open <strong>Google Play Store</strong> (Android) or <strong>App Store</strong> ',
        '(iPhone), search for <strong>geteduroam</strong> and install it.</li>',
        '<li>Open the app and search for <strong>Muteesa I Royal University</strong>.</li>',
        '<li>Enter your <strong>University email address and password</strong>. Not your ',
        'personal Gmail.</li>',
        '<li>Complete the setup and confirm you are connected to <strong>eduroam</strong>.</li>',
        '<li>Turn off your mobile data. You are now on the University internet. 🥳</li>',
      '</ul>',
      '<p>Watch the video guide here: ',
      '<a href="https://www.youtube.com/watch?v=ZshomuSwkeg" target="_blank" rel="noopener">',
      'https://www.youtube.com/watch?v=ZshomuSwkeg</a></p>',
      '<p>For laptops, go to <a href="https://cat.eduroam.org" target="_blank" rel="noopener">',
      'https://cat.eduroam.org</a> and follow the same steps.</p>',
      '<p>Students without a University email can request one in the portal under ',
      '<strong>Get University Email</strong>. Anyone still stuck can visit the MIS office on ',
      'their campus.</p>',
      '<p>M. Muhindo,<br>MRU, MIS.</p>'
    ),
    'BOTH', 0, NULL, 'HIGH',
    'PUBLISHED', 1, 'muhindo', 'M. Muhindo', NOW(), NOW(),
    NOW(), 1, DATE_ADD(NOW(), INTERVAL 3 MONTH)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM sys_communications WHERE title LIKE 'How to Connect to eduroam Wi-Fi%');

-- ── 2. A home for it in the knowledge base ─────────────────────────────────
--    None of the four existing categories fits: three are portal tutorials and
--    one is appraisal. Campus Wi-Fi is neither, and filing it under "Student
--    Portal Video Tutorials" would both mislabel it and hide it from staff.
INSERT INTO sys_knowledgebase_categories
    (category_key, title, description, display_order, is_active, created_by, created_at)
SELECT 'campus-it-and-connectivity', 'Campus IT and Connectivity',
       'Guides for using the University network and campus IT services, including eduroam Wi-Fi, University email and connecting personal devices. For staff and students.',
       5, 1, 'system', NOW()
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM sys_knowledgebase_categories WHERE category_key='campus-it-and-connectivity');

-- ── 3. The video article ───────────────────────────────────────────────────
INSERT INTO sys_knowledgebase_articles
    (category_id, article_key, title, description, content, photo_path,
     is_youtube_video, youtube_url, display_order, view_count, visibility, status,
     created_by, created_at, published_at)
SELECT
    (SELECT ID FROM sys_knowledgebase_categories WHERE category_key='campus-it-and-connectivity'),
    'how-to-connect-to-eduroam-wifi',
    'How to Connect to eduroam Wi-Fi',
    'Connect your phone or laptop to the University Wi-Fi using your University email and password, so it connects automatically whenever you are on campus.',
    CONCAT(
      '<p><strong>eduroam</strong> is the University&rsquo;s secure Wi-Fi. You connect using your ',
      'University email and password, and your device connects automatically whenever you are ',
      'on campus.</p>',
      '<p><em>Before you start: be within range of eduroam and turn on your mobile data ',
      '(first time only).</em></p>',
      '<h4>On a phone</h4>',
      '<ol>',
        '<li>Open <strong>Google Play Store</strong> (Android) or <strong>App Store</strong> ',
        '(iPhone), search for <strong>geteduroam</strong> and install it.</li>',
        '<li>Open the app and search for <strong>Muteesa I Royal University</strong>.</li>',
        '<li>Enter your <strong>University email address and password</strong>. Not your ',
        'personal Gmail.</li>',
        '<li>Complete the setup and confirm you are connected to <strong>eduroam</strong>.</li>',
        '<li>Turn off your mobile data. You are now on the University internet.</li>',
      '</ol>',
      '<h4>On a laptop</h4>',
      '<p>Go to <a href="https://cat.eduroam.org" target="_blank" rel="noopener">',
      'cat.eduroam.org</a> and follow the same steps.</p>',
      '<h4>If you do not have a University email</h4>',
      '<p>Students can request one in the portal under <strong>Get University Email</strong>. ',
      'Anyone still stuck can visit the MIS office on their campus.</p>'
    ),
    NULL, 1, 'https://www.youtube.com/watch?v=ZshomuSwkeg', 1, 0, 'BOTH', 'PUBLISHED',
    'muhindo', NOW(), NOW()
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM sys_knowledgebase_articles WHERE article_key='how-to-connect-to-eduroam-wifi');

SELECT
  (SELECT COUNT(*) FROM sys_communications WHERE title LIKE 'How to Connect to eduroam%') AS notice,
  (SELECT COUNT(*) FROM sys_knowledgebase_categories WHERE category_key='campus-it-and-connectivity') AS category,
  (SELECT COUNT(*) FROM sys_knowledgebase_articles WHERE article_key='how-to-connect-to-eduroam-wifi') AS article;
