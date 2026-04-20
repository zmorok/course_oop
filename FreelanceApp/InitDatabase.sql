CREATE SCHEMA core AUTHORIZATION svc_admin;
ALTER ROLE svc_admin SET search_path = core;
ALTER ROLE svc_app SET search_path = core;
ALTER ROLE app_end_usr SET search_path = core;

CREATE TABLE IF NOT EXISTS core.roles (
    id_role SERIAL PRIMARY KEY,
    role_name VARCHAR(50) NOT NULL UNIQUE,
    role_privileges JSONB NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE core.users (
    id_user SERIAL PRIMARY KEY,
    password CHAR(128) NOT NULL CHECK (length(password) = 128),
    role INTEGER NOT NULL REFERENCES core.roles(id_role) ON UPDATE CASCADE ON DELETE RESTRICT,
    last_name  VARCHAR(100) NOT NULL CHECK (char_length(last_name)  > 0),
    first_name VARCHAR(100) NOT NULL CHECK (char_length(first_name) > 0),
    middle_name VARCHAR(100),
    gender VARCHAR(10) CHECK (gender IN ('Male','Female','Other')),
    phone_number VARCHAR(20) CHECK (phone_number ~ '^\+?[0-9]{7,20}$'),
    email VARCHAR(100) NOT NULL UNIQUE CHECK (email ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$'),
    registration_date TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_online_time TIMESTAMP CHECK (last_online_time IS NULL OR last_online_time >= registration_date),
    rating NUMERIC(2,1) DEFAULT 0.0 CHECK (rating >= 0 AND rating <= 5),
    photo BYTEA DEFAULT NULL
);

CREATE TABLE core.projects (
    id_project SERIAL PRIMARY KEY,
    id_customer INTEGER NOT NULL REFERENCES core.users(id_user) ON UPDATE CASCADE ON DELETE CASCADE,
    title VARCHAR(200) NOT NULL CHECK (char_length(title) > 0),
    status VARCHAR(50) NOT NULL CHECK (status IN ('draft','open','in_progress','completed','cancelled')),
    description TEXT NOT NULL CHECK (char_length(description) > 0),
    media JSONB
);

CREATE TABLE core.orders_archive (
    id_order_arc SERIAL PRIMARY KEY,
    id_order INT NOT NULL,
    id_project INT NOT NULL,
    project_title TEXT NOT NULL,
    id_customer INT NOT NULL REFERENCES core.users(id_user) ON UPDATE CASCADE ON DELETE CASCADE,
    id_freelancer INT REFERENCES core.users(id_user) ON UPDATE CASCADE ON DELETE CASCADE,
    status VARCHAR(50) CHECK (status IN ('completed','cancelled')),
    creation_date TIMESTAMP,
    deadline DATE
);

CREATE TABLE core.reviews (
    id_review SERIAL PRIMARY KEY,
    id_order INT NOT NULL REFERENCES core.orders_archive(id_order_arc) ON UPDATE CASCADE ON DELETE CASCADE,
    id_author INT NOT NULL REFERENCES core.users(id_user) ON UPDATE CASCADE ON DELETE CASCADE,
    id_recipient INT REFERENCES core.users(id_user) ON UPDATE CASCADE ON DELETE SET NULL,
    comment TEXT NOT NULL CHECK (char_length(comment) > 0),
    rating INT NOT NULL CHECK (rating BETWEEN 1 AND 5),
    media JSONB,
	
    CONSTRAINT uq_review_per_side UNIQUE (id_order, id_author),
    CONSTRAINT ck_no_self_review CHECK (id_recipient IS NULL OR id_author <> id_recipient)
);

CREATE TABLE core.orders (
    id_order SERIAL PRIMARY KEY,
    id_project    INTEGER NOT NULL UNIQUE REFERENCES core.projects(id_project) ON UPDATE CASCADE ON DELETE CASCADE,
    id_freelancer INTEGER NOT NULL REFERENCES core.users(id_user)     ON UPDATE CASCADE ON DELETE CASCADE,
    status VARCHAR(50) NOT NULL CHECK (status IN ('pending','active','completed','cancelled','disputed')),
    creation_date TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deadline DATE CHECK (deadline IS NULL OR deadline::timestamp >= creation_date)
);

CREATE TABLE core.complaints (
    id_complaint SERIAL PRIMARY KEY,
    id_user INTEGER NOT NULL REFERENCES core.users(id_user) ON UPDATE CASCADE ON DELETE CASCADE,           -- на кого жалоба
    filed_by INTEGER NOT NULL REFERENCES core.users(id_user) ON UPDATE CASCADE ON DELETE CASCADE,         -- кто подал
    processed_by_admin INTEGER REFERENCES core.users(id_user) ON UPDATE CASCADE ON DELETE SET NULL,       -- админ-обработчик
    status VARCHAR(50) NOT NULL CHECK (status IN ('new','in_progress','resolved','dismissed')),
    description TEXT NOT NULL CHECK (char_length(description) > 0),
    id_order INT REFERENCES core.orders(id_order) ON UPDATE CASCADE ON DELETE SET NULL,
    id_order_arc INT REFERENCES core.orders_archive(id_order_arc) ON UPDATE CASCADE ON DELETE SET NULL,
	media JSONB,
	
    CONSTRAINT ck_complaint_link CHECK (id_order IS NOT NULL OR id_order_arc IS NOT NULL),
    CONSTRAINT ck_no_self_complaint CHECK (id_user <> filed_by)
);
-- счётчик изменений, нужно выполнить при создании таблицы
ALTER TABLE core.complaints ADD COLUMN description_change_count smallint NOT NULL DEFAULT 0;
ALTER TABLE core.complaints ADD CONSTRAINT ck_complaints_desc_change_count CHECK (description_change_count BETWEEN 0 AND 3);


CREATE TABLE core.warnings (
  id_warning SERIAL PRIMARY KEY,
  id_user INT NOT NULL REFERENCES core.users(id_user) ON UPDATE CASCADE ON DELETE CASCADE,
  id_moderator INT NOT NULL REFERENCES core.users(id_user) ON UPDATE CASCADE ON DELETE RESTRICT,
  id_complaint INT UNIQUE NOT NULL REFERENCES core.complaints(id_complaint) ON UPDATE CASCADE ON DELETE SET NULL,
  message TEXT NOT NULL,
  issued_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  expires_at TIMESTAMP,
  is_resolved BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE core.portfolio (
    id_portfolio SERIAL PRIMARY KEY,
    id_user INTEGER NOT NULL REFERENCES core.users(id_user) ON UPDATE CASCADE ON DELETE CASCADE,
    description TEXT NOT NULL CHECK (char_length(description) > 0),
    skills TEXT[] NOT NULL CHECK (array_length(skills,1) >= 1),
    experience TEXT,
	  media JSONB
);

CREATE TABLE core.audit_logs (
    id_log SERIAL PRIMARY KEY,
    user_id INTEGER REFERENCES core.users(id_user) ON UPDATE CASCADE ON DELETE SET NULL,
	proc_name VARCHAR(100),
    action VARCHAR(10) NOT NULL CHECK (action IN ('INSERT','UPDATE','DELETE')),
    table_name VARCHAR(50) NOT NULL,
    record_id INTEGER NOT NULL,
    old_data JSONB,
    new_data JSONB,
    changed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE core.notifications (
    id_notification SERIAL PRIMARY KEY,
    id_sender   INT NOT NULL REFERENCES core.users(id_user) ON DELETE CASCADE,
    id_receiver INT NOT NULL REFERENCES core.users(id_user) ON DELETE CASCADE,
    id_project  INT NOT NULL REFERENCES core.projects(id_project) ON DELETE CASCADE,
    type VARCHAR(30) NOT NULL DEFAULT 'system' CHECK (type IN ('system','invite','status','message')),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	payload JSONB,
	
    CONSTRAINT uq_invite_per_project UNIQUE (id_receiver, id_project)
);




CREATE OR REPLACE VIEW core.v_roles AS
SELECT r.id_role, r.role_name
FROM core.roles r;

CREATE OR REPLACE VIEW core.v_users AS
SELECT
  u.id_user,
  u.role            AS id_role,
  r.role_name,
  u.last_name,
  u.first_name,
  u.middle_name,
  u.gender,
  u.registration_date,
  u.last_online_time,
  u.rating
FROM core.users u
JOIN core.roles r ON u.role = r.id_role;

CREATE OR REPLACE VIEW core.v_projects AS
SELECT
  p.id_project,
  p.id_customer,
  p.title,
  p.status,
  p.description,
  p.media
FROM core.projects p;

CREATE OR REPLACE VIEW core.v_reviews AS
SELECT
    r.id_review,
    r.id_order,
    r.id_author,
    r.id_recipient,
    r.comment,
    r.rating,
    r.media
FROM core.reviews r;

CREATE OR REPLACE VIEW core.v_orders AS
SELECT
  o.id_order,
  o.id_project,
  o.id_freelancer,
  o.status,
  o.creation_date,
  o.deadline
FROM core.orders o;

CREATE OR REPLACE VIEW core.v_order_extended AS
SELECT
  o.id_order      AS "OrderId",
  o.status        AS "OrderStatus",
  o.creation_date AS "OrderCreationDate",
  o.deadline      AS "OrderDeadline",

  p.id_project    AS "ProjectId",
  p.title         AS "ProjectTitle",
  p.status        AS "ProjectStatus",

  c.id_user       AS "CustomerId",
  (c.first_name || ' ' || c.last_name) AS "CustomerFullName",

  f.id_user       AS "FreelancerId",
  COALESCE(f.first_name || ' ' || f.last_name, 'Не назначен') AS "FreelancerFullName"
FROM core.v_orders o
JOIN core.v_projects p ON o.id_project = p.id_project
JOIN core.v_users    c ON p.id_customer = c.id_user
LEFT JOIN core.v_users f ON o.id_freelancer = f.id_user;

CREATE OR REPLACE VIEW core.v_orders_archive AS
SELECT 
  a.id_order_arc,
  a.id_order,
  a.id_project,
  a.project_title,
  a.id_customer,
  a.id_freelancer,
  a.status,
  a.creation_date,
  a.deadline
FROM core.orders_archive a;

CREATE OR REPLACE VIEW core.v_order_archive_extended AS
SELECT
  a.id_order_arc	  AS "OrderArcId",
  a.id_order          AS "OrderId",
  a.status            AS "OrderStatus",
  a.creation_date     AS "OrderCreationDate",
  a.deadline          AS "OrderDeadline",

  a.id_project        AS "ProjectId",
  a.project_title     AS "ProjectTitle",
  'archived'          AS "ProjectStatus",

  a.id_customer       AS "CustomerId",
  COALESCE(c.first_name || ' ' || c.last_name, 'Неизвестно') AS "CustomerFullName",

  a.id_freelancer     AS "FreelancerId",
  COALESCE(f.first_name || ' ' || f.last_name, 'Не назначен') AS "FreelancerFullName"
FROM core.orders_archive a
LEFT JOIN core.v_users f ON f.id_user = a.id_freelancer
LEFT JOIN core.v_users c ON c.id_user = a.id_customer;

CREATE OR REPLACE VIEW core.v_portfolio AS
SELECT
  pf.id_portfolio,
  pf.id_user,
  pf.description,
  pf.media,
  pf.skills,
  pf.experience
FROM core.portfolio pf;

CREATE OR REPLACE VIEW core.v_orders_reviews AS
SELECT
    oa.id_order_arc                              AS order_id,
    oa.creation_date,

    oa.project_title,

    oa.id_customer,
    (cu.first_name || ' ' || cu.last_name)       AS customer_fullname,

    oa.id_freelancer,
    (fu.first_name || ' ' || fu.last_name)       AS freelancer_fullname,

    cr.id_review        AS customer_review_id,
    cr.comment          AS customer_comment,
    cr.rating           AS customer_rating,

    fr.id_review        AS freelancer_review_id,
    fr.comment          AS freelancer_comment,
    fr.rating           AS freelancer_rating,
	
	cr.media			AS customer_media,
	fr.media			AS freelancer_media
FROM   core.orders_archive  oa
JOIN   core.v_users         cu  ON cu.id_user = oa.id_customer
LEFT   JOIN core.v_users    fu  ON fu.id_user = oa.id_freelancer
LEFT   JOIN core.v_reviews  cr  ON cr.id_order   = oa.id_order_arc
                               AND cr.id_author  = oa.id_customer
LEFT   JOIN core.v_reviews  fr  ON fr.id_order   = oa.id_order_arc
                               AND fr.id_author  = oa.id_freelancer;

CREATE OR REPLACE VIEW core.v_all_counterparts AS
SELECT DISTINCT
  u.id_user                         AS user_id,
  CASE
    WHEN oa.id_customer = u.id_user   THEN oa.id_freelancer
    ELSE oa.id_customer
  END                               AS counterpart_id
FROM core.orders_archive oa
JOIN core.users u ON u.id_user IN (oa.id_customer, oa.id_freelancer);

CREATE OR REPLACE VIEW core.v_complaints AS
SELECT
  c.id_complaint,
  c.id_user,
  c.filed_by,
  c.processed_by_admin,
  c.status,
  c.description,
  c.media,
  c.id_order,
  c.id_order_arc,
  c.description_change_count
FROM core.complaints c;

CREATE OR REPLACE VIEW core.v_user_notifications AS
SELECT
    n.id_notification,
    n.id_project,
    p.title  AS project_title,
    n.id_sender,
    (s.first_name || ' ' || s.last_name) AS sender_name,
    n.id_receiver,
    (r.first_name || ' ' || r.last_name) AS receiver_name,
    n.type,
    n.payload,
    n.created_at
FROM core.notifications n
JOIN core.projects p ON p.id_project = n.id_project
JOIN core.users   s ON s.id_user = n.id_sender
JOIN core.users   r ON r.id_user = n.id_receiver;

CREATE OR REPLACE VIEW core.v_user_warnings AS
SELECT
  w.id_warning,
  w.id_user                         AS user_id,
  (u.first_name || ' ' || u.last_name) AS user_name,
  w.issued_by_admin                 AS admin_id,
  (a.first_name || ' ' || a.last_name) AS admin_name,
  w.id_complaint,
  w.message,
  w.issued_at,
  w.expires_at,
  w.is_resolved
FROM core.warnings w
JOIN core.users u ON w.id_user = u.id_user
JOIN core.users a ON w.issued_by_admin = a.id_user;

CREATE OR REPLACE VIEW core.v_admin_users AS
SELECT
  u.id_user,
  u.role           AS id_role,
  r.role_name,
  u.last_name,
  u.first_name,
  u.middle_name,
  u.gender,
  u.phone_number,
  u.email,
  u.registration_date,
  u.last_online_time,
  u.rating
FROM core.users u
JOIN core.roles r ON u.role = r.id_role;

-- A2. Жалобы для админ-панели (с именами сторон и обработчиком)
CREATE OR REPLACE VIEW core.v_admin_complaints AS
SELECT
  c.id_complaint,
  c.id_user                         AS "UserComId",
  (u1.first_name || ' ' || u1.last_name) AS "UserComName",
  c.filed_by                        AS "FiledById",
  (u2.first_name || ' ' || u2.last_name) AS "FiledByName",
  c.processed_by_admin              AS "ProcessedByAdminId",
  (ua.first_name || ' ' || ua.last_name) AS "ProcessedByAdminName",
  c.status                          AS "Status",
  c.description                     AS "Description",
  c.media                           AS "Media",
  c.id_order,
  c.id_order_arc
FROM core.complaints c
JOIN core.users u1 ON c.id_user  = u1.id_user
JOIN core.users u2 ON c.filed_by = u2.id_user
LEFT JOIN core.users ua ON c.processed_by_admin = ua.id_user
WHERE c.id_user <> c.filed_by;




CREATE OR REPLACE FUNCTION core.get_schema_report(p_schema TEXT DEFAULT 'core')
RETURNS TABLE(table_name TEXT,row_count BIGINT,total_size TEXT)
LANGUAGE plpgsql STABLE AS $$
DECLARE rec RECORD; cnt BIGINT; sz TEXT;
BEGIN
  FOR rec IN
    SELECT t.table_name AS tbl
      FROM information_schema.tables AS t
     WHERE t.table_schema = p_schema
       AND t.table_type   = 'BASE TABLE'
     ORDER BY t.table_name
  LOOP
    EXECUTE format('SELECT count(*) FROM %I.%I', p_schema, rec.tbl) INTO cnt;

    EXECUTE format(
      'SELECT pg_size_pretty(pg_total_relation_size(%L))',
      p_schema || '.' || rec.tbl
    ) INTO sz;

    table_name := rec.tbl;
    row_count   := cnt;
    total_size  := sz;
    RETURN NEXT;
  END LOOP;
END; $$;

CREATE OR REPLACE PROCEDURE core.export_schema_report(p_schema TEXT, p_path TEXT, p_format TEXT DEFAULT 'csv')
LANGUAGE plpgsql SECURITY DEFINER AS $body$
DECLARE actual_path TEXT; ts TEXT := to_char(now(), 'YYYYMMDD_HH24MISS');
BEGIN
  IF p_path IS NULL OR btrim(p_path) = '' THEN RAISE EXCEPTION 'Не указан путь для экспорта.'; END IF;

  IF right(p_path,1) IN ('/','\') THEN
    actual_path := p_path || p_schema || '_' || ts || '.' || lower(p_format);
  ELSE
    actual_path := p_path;
  END IF;

  CASE lower(p_format)
    WHEN 'csv'  THEN EXECUTE format(
      'COPY (SELECT * FROM core.get_schema_report(%L)) TO %L WITH (FORMAT csv, HEADER true)',
      p_schema, actual_path);

    WHEN 'txt'  THEN EXECUTE format(
      'COPY (SELECT * FROM core.get_schema_report(%L)) TO %L WITH (FORMAT text, DELIMITER E''\t'', HEADER true)',
      p_schema, actual_path);

    WHEN 'json' THEN EXECUTE format(
      'COPY (SELECT json_agg(t) FROM (SELECT * FROM core.get_schema_report(%L)) AS t) TO %L',
      p_schema, actual_path);

    WHEN 'html' THEN EXECUTE format($html$
      COPY (
        SELECT '<!DOCTYPE html>'
        UNION ALL SELECT '<html lang="ru">'
        UNION ALL SELECT '<head>'
        UNION ALL SELECT '<meta charset="UTF-8">'
        UNION ALL SELECT format('<title>Отчет по схеме %s</title>', %1$L)
        UNION ALL SELECT '<style>table{border-collapse:collapse;}th,td{border:1px solid #999;padding:4px;}</style>'
        UNION ALL SELECT '</head>'
        UNION ALL SELECT '<body>'
        UNION ALL SELECT format('<h2>Схема: %s (от %s)</h2>', %1$L, %2$L)
        UNION ALL SELECT '<table>'
        UNION ALL SELECT '<tr><th>Таблица</th><th>Строк</th><th>Размер</th></tr>'
        UNION ALL
          SELECT format('<tr><td>%s</td><td align="right">%s</td><td align="right">%s</td></tr>',
                        table_name, row_count, total_size)
          FROM core.get_schema_report(%1$L)
        UNION ALL SELECT '</table>'
        UNION ALL SELECT '</body>'
        UNION ALL SELECT '</html>'
      ) TO %3$L $html$, p_schema, ts, actual_path);

    ELSE RAISE EXCEPTION 'Неподдерживаемый формат: %, допустимы CSV, TXT, JSON, HTML.', p_format;
  END CASE;

  RAISE NOTICE 'Отчет по схеме "%" экспортирован в "%". Формат: %', p_schema, actual_path, p_format;
END; $body$;

CREATE OR REPLACE PROCEDURE core.update_user_last_online (p_performer_id INT, p_user_id INTEGER)
LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.update_user_last_online', false);

  UPDATE core.users SET last_online_time = CURRENT_TIMESTAMP WHERE id_user = p_user_id;
EXCEPTION WHEN OTHERS THEN
  RAISE EXCEPTION 'Ошибка при сохранении времени последнего входа. Пожалуйста, попробуйте позже.';
END; $$;

CREATE OR REPLACE FUNCTION core.search_users(current_user_id INTEGER, query TEXT DEFAULT '')
RETURNS TABLE (id INTEGER, "FullName" TEXT, "SkillsPreview" TEXT, "CanInvite" BOOLEAN, "Media" JSONB)
LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
DECLARE v_query TEXT := COALESCE(query, '');
BEGIN
  IF current_user_id IS NULL THEN RAISE EXCEPTION 'Не указан текущий пользователь.'; END IF;

  RETURN QUERY
  SELECT  
    u.id_user AS id,
    u.first_name || ' ' || u.last_name 	AS "FullName",
    COALESCE(sk.skills_preview, '')     AS "SkillsPreview",
    NOT EXISTS (
      SELECT 1
        FROM core.notifications n
       WHERE n.id_sender   = current_user_id
         AND n.id_receiver = u.id_user
         AND n.type        = 'invite'
    ) 									AS "CanInvite",
	p.media                             AS "Media"
  FROM core.v_users u
  LEFT JOIN core.v_portfolio p ON p.id_user = u.id_user
  LEFT JOIN LATERAL (
    SELECT string_agg(DISTINCT skill, ', ') AS skills_preview
      FROM unnest(p.skills) AS skill
  ) sk ON TRUE
  WHERE u.id_user <> current_user_id
    AND u.role_name = 'user'
    AND (
      v_query = ''
      OR lower(u.first_name || ' ' || u.last_name) LIKE '%' || lower(v_query) || '%'
      OR EXISTS (SELECT 1 FROM unnest(p.skills) AS skill WHERE lower(skill) LIKE '%' || lower(v_query) || '%')
    )
  ORDER BY u.first_name, u.last_name;
EXCEPTION WHEN OTHERS THEN
  RAISE EXCEPTION 'Ошибка при поиске пользователей. Пожалуйста, попробуйте чуть позже.';
END; $$;

CREATE OR REPLACE FUNCTION core.free_projects(current_user_id INTEGER, freelancer_id INTEGER)
RETURNS SETOF core.v_projects
LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
BEGIN
  IF current_user_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор текущего пользователя.'; END IF;
  IF freelancer_id   IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор фрилансера.'; END IF;

  RETURN QUERY
    SELECT p.*
      FROM core.v_projects p
     WHERE p.id_customer = current_user_id
       AND NOT EXISTS (
         SELECT 1
           FROM core.notifications n
          WHERE n.id_project  = p.id_project
            AND n.id_receiver = freelancer_id
            AND n.type        = 'invite'
       );
EXCEPTION WHEN OTHERS THEN
  RAISE EXCEPTION 'Ошибка при получении списка свободных проектов. Пожалуйста, попробуйте позже.';
END; $$;


CREATE OR REPLACE FUNCTION core.user_counterparts(p_user_id INTEGER)
RETURNS TABLE ("Id" INTEGER, "FullName" TEXT)
LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
BEGIN
  IF p_user_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;

  RETURN QUERY
    SELECT DISTINCT u.id_user AS "Id", u.first_name || ' ' || u.last_name AS "FullName"
    FROM core.v_all_counterparts cp
    JOIN core.v_users u ON u.id_user = cp.counterpart_id
    WHERE cp.counterpart_id <> p_user_id
    AND cp.user_id = p_user_id
    AND cp.user_id IN (
      SELECT id_customer  FROM core.v_orders_archive WHERE id_freelancer = cp.counterpart_id
      UNION
      SELECT id_freelancer FROM core.v_orders_archive WHERE id_customer   = cp.counterpart_id
    );
EXCEPTION WHEN OTHERS THEN
  RAISE EXCEPTION 'Ошибка при получении списка контрагентов. Пожалуйста, попробуйте чуть позже.';
END; $$;

CREATE OR REPLACE PROCEDURE core.admin_create_user (
  p_performer_id INT,
  p_password     CHAR(128),
  p_role         INT,
  p_last_name    VARCHAR,
  p_first_name   VARCHAR,
  p_middle_name  VARCHAR DEFAULT NULL,
  p_gender       VARCHAR DEFAULT NULL,
  p_phone        VARCHAR DEFAULT NULL,
  p_email        VARCHAR DEFAULT NULL,
  p_rating       NUMERIC(2,1) DEFAULT 0.0
) LANGUAGE plpgsql SECURITY DEFINER AS $$
DECLARE
  v_state   text;
  v_msg     text;
  v_detail  text;
  v_hint    text;
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор администратора.';END IF;
  IF p_password IS NULL OR length(trim(p_password)) = 0 THEN RAISE EXCEPTION 'Не указан пароль нового пользователя.'; END IF;
  IF p_role IS NULL THEN RAISE EXCEPTION 'Не задана роль нового пользователя.'; END IF;
  IF p_last_name  IS NULL OR btrim(p_last_name)  = '' THEN RAISE EXCEPTION 'Не указана фамилия нового пользователя.'; END IF;
  IF p_first_name IS NULL OR btrim(p_first_name) = '' THEN RAISE EXCEPTION 'Не указано имя нового пользователя.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.admin_create_user', false);

  INSERT INTO core.users (password,role,last_name,first_name,middle_name,gender,phone_number,email,rating)
  VALUES (p_password,p_role,p_last_name,p_first_name,p_middle_name,p_gender,p_phone,p_email,p_rating);
EXCEPTION WHEN OTHERS THEN
  GET STACKED DIAGNOSTICS 
  		v_state  = RETURNED_SQLSTATE,
      	v_msg    = MESSAGE_TEXT,
      	v_detail = PG_EXCEPTION_DETAIL,
      	v_hint   = PG_EXCEPTION_HINT;
  RAISE EXCEPTION USING
      ERRCODE = v_state,
      MESSAGE = format('Ошибка при создании пользователя: %s', v_msg),
      DETAIL  = v_detail,
      HINT    = v_hint;
END; $$;

CREATE OR REPLACE PROCEDURE core.admin_update_user (
  p_performer_id INTEGER,
  p_id           INTEGER,
  p_password     CHAR(128)    DEFAULT NULL,
  p_role         INTEGER      DEFAULT NULL,
  p_last_name    VARCHAR      DEFAULT NULL,
  p_first_name   VARCHAR      DEFAULT NULL,
  p_middle_name  VARCHAR      DEFAULT NULL,
  p_gender       VARCHAR      DEFAULT NULL,
  p_phone        VARCHAR      DEFAULT NULL,
  p_email        VARCHAR      DEFAULT NULL,
  p_rating       NUMERIC(2,1) DEFAULT NULL
) LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор администратора.'; END IF;
  IF p_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя для обновления.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.admin_update_user', false);

  UPDATE core.users
     SET password     = COALESCE(p_password,     password),
         role         = COALESCE(p_role,         role),
         last_name    = COALESCE(p_last_name,    last_name),
         first_name   = COALESCE(p_first_name,   first_name),
         middle_name  = COALESCE(p_middle_name,  middle_name),
         gender       = COALESCE(p_gender,       gender),
         phone_number = COALESCE(p_phone,        phone_number),
         email        = COALESCE(p_email,        email),
         rating       = COALESCE(p_rating,       rating)
   WHERE id_user = p_id;
  IF NOT FOUND THEN RAISE EXCEPTION 'Пользователь с идентификатором % не найден.', p_id; END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE EXCEPTION 'Ошибка при обновлении данных пользователя. Пожалуйста, попробуйте позже.';
END; $$;

CREATE OR REPLACE PROCEDURE core.admin_delete_user (p_performer_id INTEGER, p_id INTEGER)
LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор администратора.'; END IF;
  IF p_id           IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя для удаления.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.admin_delete_user', false);

  DELETE FROM core.users WHERE id_user = p_id;
  IF NOT FOUND THEN RAISE EXCEPTION 'Пользователь с идентификатором % не найден.', p_id; END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE EXCEPTION 'Ошибка при удалении пользователя. Пожалуйста, попробуйте позже.';
END; $$;

CREATE OR REPLACE FUNCTION core.admin_get_users()
RETURNS SETOF core.users LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
BEGIN
  RETURN QUERY SELECT * FROM core.users ORDER BY id_user;
END; $$;

CREATE OR REPLACE FUNCTION core.admin_get_user_by_id(
  user_id INTEGER
)
  RETURNS SETOF core.users
  LANGUAGE plpgsql
  STABLE
  SECURITY DEFINER
AS $$
BEGIN
  IF p_user_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  RETURN QUERY SELECT * FROM core.users WHERE id_user = user_id;
EXCEPTION
  WHEN OTHERS THEN
    RAISE EXCEPTION 'Ошибка при получении данных пользователя. Пожалуйста, попробуйте позже.';
END;
$$;


CREATE OR REPLACE PROCEDURE core.admin_create_role (p_performer_id INTEGER, p_name VARCHAR, p_privileges JSONB)
LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор администратора.'; END IF;
  IF p_name IS NULL OR btrim(p_name) = '' THEN RAISE EXCEPTION 'Не указано название роли.'; END IF;
  IF p_privileges IS NULL THEN RAISE EXCEPTION 'Не указаны привилегии роли.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.admin_create_role', false);

  IF EXISTS (SELECT 1 FROM core.roles WHERE role_name = p_name) THEN RAISE EXCEPTION 'Роль % уже существует.', p_name; END IF;
  INSERT INTO core.roles(role_name, role_privileges) VALUES (p_name, p_privileges);
END; $$;

CREATE OR REPLACE PROCEDURE core.admin_update_role (p_performer_id INTEGER, p_id INTEGER, p_privileges JSONB)
LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор администратора.'; END IF;
  IF p_id           IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор роли.'; END IF;
  IF p_privileges   IS NULL THEN RAISE EXCEPTION 'Не заданы привилегии роли.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.admin_update_role', false);

  IF NOT EXISTS (SELECT 1 FROM core.roles WHERE id_role = p_id) THEN RAISE EXCEPTION 'Роль с id % не найдена.', p_id; END IF;
  UPDATE core.roles SET role_privileges = p_privileges WHERE id_role = p_id;
END; $$;


CREATE OR REPLACE PROCEDURE core.admin_delete_role (p_performer_id INTEGER, p_id INTEGER)
LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор администратора.'; END IF;
  IF p_id           IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор роли.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.admin_delete_role', false);

  DELETE FROM core.roles WHERE id_role = p_id;
  IF NOT FOUND THEN RAISE EXCEPTION 'Роль с идентификатором % не найдена.', p_id; END IF;
END; $$;

CREATE OR REPLACE PROCEDURE core.admin_change_user_role (p_performer_id INTEGER, p_user_id INTEGER, p_new_role INTEGER)
LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор администратора.'; END IF;
  IF p_user_id     IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_new_role    IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор новой роли.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.admin_change_user_role', false);

  IF NOT EXISTS (SELECT 1 FROM core.users WHERE id_user = p_user_id) THEN RAISE EXCEPTION 'Пользователь с id % не найден.', p_user_id; END IF;
  IF NOT EXISTS (SELECT 1 FROM core.roles WHERE id_role = p_new_role) THEN RAISE EXCEPTION 'Роль с id % не найдена.', p_new_role; END IF;

  UPDATE core.users SET role = p_new_role WHERE id_user = p_user_id;
END; $$;

CREATE OR REPLACE FUNCTION core.admin_get_roles()
RETURNS SETOF core.roles LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
BEGIN
  RETURN QUERY SELECT * FROM core.roles;
END; $$;

CREATE OR REPLACE FUNCTION core.admin_get_audit_logs(p_since TIMESTAMP DEFAULT NULL, p_until TIMESTAMP DEFAULT NULL)
RETURNS SETOF core.audit_logs LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
BEGIN
  IF p_since IS NOT NULL AND p_until IS NOT NULL AND p_since > p_until
  THEN RAISE EXCEPTION 'Начальная дата не может быть позднее конечной.'; END IF;

  RETURN QUERY
    SELECT * FROM core.audit_logs
     WHERE (p_since IS NULL OR changed_at >= p_since)
       AND (p_until IS NULL OR changed_at <= p_until)
     ORDER BY changed_at DESC;
END; $$;

CREATE OR REPLACE PROCEDURE core.admin_export_audit_logs_json(p_filepath TEXT, p_since TIMESTAMP DEFAULT NULL, p_until TIMESTAMP DEFAULT NULL)
LANGUAGE plpgsql SECURITY DEFINER AS $$
DECLARE sql TEXT;
BEGIN
  IF p_filepath IS NULL OR btrim(p_filepath) = '' THEN RAISE EXCEPTION 'Не указан путь для экспорта.'; END IF;
  IF p_since IS NOT NULL AND p_until IS NOT NULL AND p_since > p_until THEN RAISE EXCEPTION 'Начальная дата не может быть позже конечной.'; END IF;

  SET LOCAL audit.skip = 'on';

  sql := 'COPY ( SELECT COALESCE(json_agg(row_to_json(al)), ''[]'') FROM core.audit_logs al WHERE TRUE';
  IF p_since IS NOT NULL THEN sql := sql || ' AND al.changed_at >= ' || quote_literal(p_since); END IF;
  IF p_until IS NOT NULL THEN sql := sql || ' AND al.changed_at <= ' || quote_literal(p_until); END IF;
  sql := sql || ') TO ' || quote_literal(p_filepath);

  EXECUTE sql;
EXCEPTION WHEN OTHERS THEN
  RAISE WARNING 'Не удалось экспортировать логи БД: %', SQLERRM;
END; $$;

CREATE OR REPLACE PROCEDURE core.admin_import_audit_logs_json (p_filepath TEXT)
LANGUAGE plpgsql SECURITY DEFINER AS $$
DECLARE raw json; item json;
BEGIN
  IF p_filepath IS NULL OR btrim(p_filepath) = '' THEN RAISE EXCEPTION 'Не указан путь к файлу для импорта.'; END IF;

  SET LOCAL audit.skip = 'on';
  TRUNCATE core.audit_logs RESTART IDENTITY;
  raw := pg_read_file(p_filepath);
  FOR item IN SELECT * FROM json_array_elements(raw) LOOP
    INSERT INTO core.audit_logs(user_id, action, table_name, record_id, old_data, new_data, changed_at)
    VALUES (
      (item ->> 'user_id')::int,
      item ->> 'action',
      item ->> 'table_name',
      (item ->> 'record_id')::int,
      item ->  'old_data',
      item ->  'new_data',
      (item ->> 'changed_at')::timestamp
    );
  END LOOP;
EXCEPTION WHEN OTHERS THEN
  RAISE WARNING 'Не удалось импортировать логи БД: %', SQLERRM;
END; $$;

CREATE OR REPLACE FUNCTION core.admin_get_complaints(p_mode TEXT DEFAULT NULL)
RETURNS SETOF core.complaints
LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
BEGIN
  IF p_mode IS NOT NULL AND p_mode NOT IN ('all','', 'unsolved','resolved')
  THEN RAISE EXCEPTION 'Неверное значение режима: %', p_mode; END IF;

  RETURN QUERY
    SELECT *
      FROM core.complaints c
     WHERE p_mode IS NULL
        OR p_mode IN ('all','')
        OR (p_mode = 'unsolved' AND c.status IN ('new','in_progress'))
        OR (p_mode = 'resolved' AND c.status = 'resolved')
     ORDER BY c.id_complaint;
END; $$;

REATE OR REPLACE PROCEDURE core.admin_resolve_complaint (
  p_performer_id INTEGER,
  p_id_complaint INTEGER,
  p_status       VARCHAR,      -- 'resolved' | 'dismissed'
  p_admin_id     INTEGER
) LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор администратора.'; END IF;
  IF p_id_complaint IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор жалобы.'; END IF;
  IF p_status IS NULL OR p_status NOT IN ('resolved','dismissed') THEN RAISE EXCEPTION 'Недопустимый статус: %', p_status; END IF;
  IF p_admin_id IS NULL THEN RAISE EXCEPTION 'Не указан администратор-обработчик.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.admin_resolve_complaint', false);

  IF NOT EXISTS (SELECT 1 FROM core.complaints WHERE id_complaint = p_id_complaint)
  THEN RAISE EXCEPTION 'Жалоба % не найдена.', p_id_complaint; END IF;

  UPDATE core.complaints
     SET status = p_status,
         processed_by_admin = p_admin_id
   WHERE id_complaint = p_id_complaint;
END; $$;

CREATE OR REPLACE PROCEDURE core.admin_update_complaint_status(
  p_performer_id   INTEGER,
  p_id_complaint   INTEGER,
  p_new_status     VARCHAR,  -- 'new' | 'in_progress' | 'dismissed'
  p_admin_id       INTEGER
) LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор администратора.'; END IF;
  IF p_id_complaint IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор жалобы.'; END IF;
  IF p_new_status IS NULL OR p_new_status NOT IN ('new','in_progress','dismissed')
  THEN RAISE EXCEPTION 'Неверный статус: %', p_new_status; END IF;
  IF p_admin_id IS NULL THEN RAISE EXCEPTION 'Не указан администратор-обработчик.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.admin_update_complaint_status', false);

  IF NOT EXISTS (SELECT 1 FROM core.complaints WHERE id_complaint = p_id_complaint)
  THEN RAISE EXCEPTION 'Жалоба % не найдена.', p_id_complaint; END IF;

  UPDATE core.complaints
     SET status = p_new_status,
         processed_by_admin = p_admin_id
   WHERE id_complaint = p_id_complaint;
END; $$;

CREATE OR REPLACE PROCEDURE core.admin_issue_warning(
  p_performer_id  INT,
  p_complaint_id  INT,
  p_target_user   INT,
  p_message       TEXT,
  p_expires_days  INT DEFAULT 7
) LANGUAGE plpgsql SECURITY DEFINER AS $$
DECLARE v_expires TIMESTAMP;
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор администратора.'; END IF;
  IF p_complaint_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор жалобы.'; END IF;
  IF p_target_user  IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_message IS NULL OR btrim(p_message) = '' THEN RAISE EXCEPTION 'Не указано сообщение предупреждения.'; END IF;
  IF p_expires_days IS NULL OR p_expires_days < 0 THEN RAISE EXCEPTION 'Неверный срок действия: %', p_expires_days; END IF;

  IF NOT EXISTS (SELECT 1 FROM core.complaints WHERE id_complaint = p_complaint_id) THEN RAISE EXCEPTION 'Жалоба % не найдена.', p_complaint_id; END IF;
  IF NOT EXISTS (SELECT 1 FROM core.users WHERE id_user = p_target_user) THEN RAISE EXCEPTION 'Пользователь % не найден.', p_target_user; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.admin_issue_warning', false);

  v_expires := current_timestamp + (p_expires_days || ' days')::interval;

  IF EXISTS (SELECT 1 FROM core.warnings WHERE id_complaint = p_complaint_id) THEN RAISE EXCEPTION 'На эту жалобу уже выдано предупреждение.'; END IF;

  INSERT INTO core.warnings(id_user, issued_by_admin, id_complaint, message, expires_at, is_resolved)
  VALUES (p_target_user, p_performer_id, p_complaint_id, p_message, v_expires, FALSE);
END; $$;

CREATE OR REPLACE FUNCTION core.admin_check_user(p_performer_id INT, p_target_user INT)
RETURNS JSONB LANGUAGE plpgsql SECURITY DEFINER AS $$
DECLARE v_result JSONB;
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор администратора.'; END IF;
  IF p_target_user  IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF NOT EXISTS (SELECT 1 FROM core.users WHERE id_user = p_target_user) THEN RAISE EXCEPTION 'Пользователь % не найден.', p_target_user; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.admin_check_user', false);

  SELECT jsonb_build_object(
    'user', jsonb_build_object(
              'id',         u.id_user,
              'first_name', u.first_name,
              'last_name',  u.last_name,
              'email',      u.email,
              'rating',     u.rating
            ),
    'portfolio', COALESCE((
       SELECT jsonb_agg(jsonb_build_object(
         'id',          p.id_portfolio,
         'description', p.description,
         'media',       p.media,
         'skills',      p.skills,
         'experience',  p.experience
       ))
       FROM core.portfolio p WHERE p.id_user = p_target_user
    ), '[]'::jsonb),
    'reviews', COALESCE((
       SELECT jsonb_agg(jsonb_build_object(
         'id',            r.id_review,
         'comment',       r.comment,
         'rating',        r.rating,
         'author_id',     r.id_author,
         'author_name',   au.first_name || ' ' || au.last_name,
         'project_id',    oa.id_project,
         'project_title', oa.project_title
       ))
       FROM core.reviews r
       JOIN core.orders_archive oa ON r.id_order = oa.id_order_arc
       JOIN core.users au         ON r.id_author = au.id_user
       WHERE r.id_recipient = p_target_user
    ), '[]'::jsonb)
  ) INTO v_result
  FROM core.users u
  WHERE u.id_user = p_target_user;

  RETURN v_result;
END; $$;

CREATE OR REPLACE FUNCTION core.user_get_users()
RETURNS TABLE (id_user INTEGER, last_name VARCHAR(100), first_name VARCHAR(100), middle_name VARCHAR(100), gender VARCHAR(10), email VARCHAR(100), registration_date TIMESTAMP, last_online_time TIMESTAMP, rating DECIMAL(2,1))
LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
BEGIN
  RETURN QUERY
    SELECT id_user, last_name, first_name, middle_name, gender, email, registration_date, last_online_time, rating
    FROM core.users;
END; $$;

CREATE OR REPLACE PROCEDURE core.user_create_project (p_performer_id INTEGER, p_customer INTEGER, p_title VARCHAR, p_status VARCHAR DEFAULT 'draft', p_description TEXT DEFAULT NULL, p_media JSONB DEFAULT NULL)
LANGUAGE plpgsql SECURITY DEFINER AS $$
DECLARE
  v_state   text;
  v_msg     text;
  v_detail  text;
  v_hint    text;
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_customer    IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор заказчика.'; END IF;
  IF p_title IS NULL OR btrim(p_title) = '' THEN RAISE EXCEPTION 'Не указано название проекта.'; END IF;
  IF p_status IS NULL OR btrim(p_status) = '' THEN RAISE EXCEPTION 'Не указан статус проекта.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_create_project', false);

  INSERT INTO core.projects(id_customer,title,status,description,media)
  VALUES(p_customer,p_title,p_status,p_description,p_media);
EXCEPTION WHEN OTHERS THEN
  GET STACKED DIAGNOSTICS 
  		v_state  = RETURNED_SQLSTATE,
      	v_msg    = MESSAGE_TEXT,
      	v_detail = PG_EXCEPTION_DETAIL,
      	v_hint   = PG_EXCEPTION_HINT;
  RAISE EXCEPTION USING
      ERRCODE = v_state,
      MESSAGE = format('Ошибка при создании проекта: %s', v_msg),
      DETAIL  = v_detail,
      HINT    = v_hint;
END; $$;

CREATE OR REPLACE PROCEDURE core.user_update_project (p_performer_id INTEGER, p_id INTEGER, p_title VARCHAR DEFAULT NULL, p_status VARCHAR DEFAULT NULL, p_description TEXT DEFAULT NULL, p_media JSONB DEFAULT NULL)
LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_id           IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор проекта.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_update_project', false);

  UPDATE core.projects
     SET title=COALESCE(p_title,title),
         status=COALESCE(p_status,status),
         description=COALESCE(p_description,description),
         media=COALESCE(p_media,media)
   WHERE id_project=p_id;
  IF NOT FOUND THEN RAISE EXCEPTION 'Проект с идентификатором % не найден.', p_id; END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE EXCEPTION 'Ошибка при обновлении проекта. Пожалуйста, попробуйте позже.';
END; $$;

CREATE OR REPLACE PROCEDURE core.user_delete_project (p_performer_id INTEGER, p_id INTEGER)
LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_id           IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор проекта.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_delete_project', false);

  DELETE FROM core.projects WHERE id_project=p_id;
  IF NOT FOUND THEN RAISE EXCEPTION 'Проект с идентификатором % не найден.', p_id; END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE EXCEPTION 'Ошибка при удалении проекта. Пожалуйста, попробуйте позже.';
END; $$;

CREATE OR REPLACE FUNCTION core.user_get_projects_by_customer(p_customer INTEGER)
RETURNS SETOF core.projects LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
BEGIN
  IF p_customer IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор заказчика.'; END IF;
  RETURN QUERY SELECT * FROM core.projects WHERE id_customer = p_customer ORDER BY id_project;
END; $$;

CREATE OR REPLACE FUNCTION core.user_get_projects()
RETURNS SETOF core.projects LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
BEGIN
  RETURN QUERY SELECT * FROM core.projects ORDER BY id_project;
END; $$;

CREATE OR REPLACE FUNCTION core.user_get_projects_by_status(p_status VARCHAR(50))
RETURNS TABLE (id_project INTEGER, id_customer INTEGER, title VARCHAR, description TEXT, media JSONB)
LANGUAGE plpgsql STABLE PARALLEL SAFE SECURITY DEFINER AS $$
BEGIN
  IF p_status IS NULL OR btrim(p_status) = '' THEN RAISE EXCEPTION 'Не указан статус проектов.'; END IF;
  RETURN QUERY SELECT p.id_project, p.id_customer, p.title, p.description, p.media FROM core.projects p WHERE p.status = p_status;
END; $$;

CREATE OR REPLACE FUNCTION core.user_get_project_by_id(p_id INTEGER)
RETURNS core.projects LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
DECLARE result core.projects%ROWTYPE;
BEGIN
  IF p_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор проекта.'; END IF;
  SELECT * INTO result FROM core.projects WHERE id_project = p_id;
  IF NOT FOUND THEN RAISE EXCEPTION 'Проект с идентификатором % не найден.', p_id; END IF;
  RETURN result;
END; $$;

CREATE OR REPLACE PROCEDURE core.user_create_order (p_performer_id INTEGER, p_project INTEGER, p_freelancer INTEGER, p_status VARCHAR DEFAULT 'pending', p_deadline DATE DEFAULT NULL)
LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_project     IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор проекта.'; END IF;
  IF p_freelancer  IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор фрилансера.'; END IF;
  IF p_status IS NULL OR btrim(p_status) = '' THEN RAISE EXCEPTION 'Не указан статус заказа.'; END IF;
  IF p_deadline IS NOT NULL AND p_deadline < CURRENT_DATE THEN RAISE EXCEPTION 'Срок заказа не может быть в прошлом.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_create_order', false);

  IF EXISTS (SELECT 1 FROM core.orders WHERE id_project = p_project) THEN RAISE EXCEPTION 'На этот проект уже есть заказ. Повторный отклик невозможен.'; END IF;

  INSERT INTO core.orders (id_project, id_freelancer, status, deadline)
  VALUES (p_project, p_freelancer, p_status, p_deadline);
END; $$;

CREATE OR REPLACE PROCEDURE core.user_update_order (p_performer_id INTEGER, p_id INTEGER, p_status VARCHAR DEFAULT NULL, p_deadline DATE DEFAULT NULL)
LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_id           IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор заказа.'; END IF;
  IF p_status IS NOT NULL AND btrim(p_status) = '' THEN RAISE EXCEPTION 'Неверный статус заказа.'; END IF;
  IF p_deadline IS NOT NULL AND p_deadline < CURRENT_DATE THEN RAISE EXCEPTION 'Срок заказа не может быть раньше текущей даты.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_update_order', false);

  UPDATE core.orders
     SET status   = COALESCE(p_status, status),
         deadline = COALESCE(p_deadline, deadline)
   WHERE id_order = p_id;
  IF NOT FOUND THEN RAISE EXCEPTION 'Заказ с идентификатором % не найден.', p_id; END IF;

  IF p_status IN ('completed','cancelled') THEN CALL core.user_archive_order(p_performer_id, p_id); END IF;
END; $$;

CREATE OR REPLACE PROCEDURE core.user_archive_order (p_performer_id int, p_order int)
LANGUAGE plpgsql SECURITY DEFINER AS $$
DECLARE v_order core.orders%ROWTYPE; v_proj core.projects%ROWTYPE;
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_order IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор заказа.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_archive_order', false);

  SELECT * INTO v_order FROM core.orders   WHERE id_order = p_order;      IF NOT FOUND THEN RAISE EXCEPTION 'Заказ % не найден', p_order; END IF;
  SELECT * INTO v_proj  FROM core.projects WHERE id_project = v_order.id_project; IF NOT FOUND THEN RAISE EXCEPTION 'Проект % не найден.', v_order.id_project; END IF;

  INSERT INTO core.orders_archive (id_order, id_project, id_customer, id_freelancer, status, creation_date, deadline, project_title)
  VALUES (v_order.id_order, v_order.id_project, v_proj.id_customer, v_order.id_freelancer, v_order.status, v_order.creation_date, v_order.deadline, v_proj.title)
  ON CONFLICT DO NOTHING;

  IF v_order.status = 'completed' THEN
    DELETE FROM core.projects WHERE id_project = v_order.id_project;
  ELSE
    DELETE FROM core.orders WHERE id_order = v_order.id_order;
  END IF;
END; $$;

CREATE OR REPLACE PROCEDURE core.user_delete_order (p_performer_id INT, p_id INT)
LANGUAGE plpgsql SECURITY DEFINER AS $$
DECLARE v_status TEXT;
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор заказа.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_delete_order', false);

  SELECT status INTO v_status FROM core.orders WHERE id_order=p_id;
  IF NOT FOUND THEN RAISE EXCEPTION 'Заказ % не найден', p_id; END IF;

  IF v_status IN ('completed','cancelled') THEN
    CALL core.user_archive_order(p_performer_id, p_id);
  ELSE
    UPDATE core.orders SET status = 'cancelled' WHERE id_order=p_id;
    CALL core.user_archive_order(p_performer_id, p_id);
  END IF;
END; $$;

CREATE OR REPLACE FUNCTION core.user_get_orders_by_customer(p_customer INTEGER)
RETURNS SETOF core.orders LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
BEGIN
  IF p_customer IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор заказчика.'; END IF;
  RETURN QUERY
    SELECT o.* FROM core.orders o
    JOIN core.projects p ON o.id_project = p.id_project
    WHERE p.id_customer = p_customer
    ORDER BY o.id_order;
END; $$;

CREATE OR REPLACE FUNCTION core.user_get_orders_by_freelancer(p_freelancer INTEGER)
RETURNS SETOF core.orders LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
BEGIN
  IF p_freelancer IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор фрилансера.'; END IF;
  RETURN QUERY SELECT * FROM core.orders WHERE id_freelancer = p_freelancer ORDER BY id_order;
END; $$;

CREATE OR REPLACE FUNCTION core.user_get_orders()
RETURNS SETOF core.orders LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
BEGIN
  RETURN QUERY SELECT * FROM core.orders ORDER BY id_order;
END; $$;

CREATE OR REPLACE PROCEDURE core.user_create_review (p_performer_id INT, p_order_arc INT, p_author INT, p_comment TEXT, p_rating INT, p_media JSONB DEFAULT NULL)
LANGUAGE plpgsql SECURITY DEFINER AS $$
DECLARE v_arch core.orders_archive%ROWTYPE; v_rec INT;
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_order_arc IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор заказа.'; END IF;
  IF p_author    IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор автора отзыва.'; END IF;
  IF p_comment IS NULL OR btrim(p_comment) = '' THEN RAISE EXCEPTION 'Не указан текст отзыва.'; END IF;
  IF p_rating  IS NULL OR p_rating < 1 OR p_rating > 5 THEN RAISE EXCEPTION 'Неверный рейтинг: % (1..5).', p_rating; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_create_review', false);

  SELECT * INTO v_arch FROM core.orders_archive WHERE id_order_arc = p_order_arc;
  IF NOT FOUND THEN RAISE EXCEPTION 'Заказ % в архиве не найден', p_order_arc; END IF;
  IF p_author NOT IN (v_arch.id_customer, v_arch.id_freelancer) THEN RAISE EXCEPTION 'Пользователь % не участвовал в заказе %', p_author, p_order_arc; END IF;

  v_rec := CASE WHEN p_author = v_arch.id_customer THEN v_arch.id_freelancer ELSE v_arch.id_customer END;

  INSERT INTO core.reviews (id_order, id_author, id_recipient, comment, rating, media)
  VALUES (p_order_arc, p_author, v_rec, p_comment, p_rating, p_media);
END; $$;

CREATE OR REPLACE PROCEDURE core.user_update_review (p_performer_id INT, p_id INT, p_author INT, p_comment TEXT DEFAULT NULL, p_rating INT DEFAULT NULL, p_media JSONB DEFAULT NULL)
LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор отзыва.'; END IF;
  IF p_author IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор автора.'; END IF;
  IF p_rating IS NOT NULL AND (p_rating < 1 OR p_rating > 5) THEN RAISE EXCEPTION 'Неверный рейтинг: % (1..5).', p_rating; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_update_review', false);

  UPDATE core.reviews
     SET comment = COALESCE(p_comment, comment),
         rating  = COALESCE(p_rating,  rating ),
         media   = COALESCE(p_media,   media  )
   WHERE id_review = p_id AND id_author = p_author;
  IF NOT FOUND THEN RAISE EXCEPTION 'Отзыв % не найден или не принадлежит пользователю %', p_id, p_author; END IF;
END; $$;

CREATE OR REPLACE PROCEDURE core.user_delete_review (p_performer_id INT, p_id INT, p_author INT)
LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор отзыва.'; END IF;
  IF p_author IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор автора.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_delete_review', false);

  DELETE FROM core.reviews WHERE id_review = p_id AND id_author = p_author;
  IF NOT FOUND THEN RAISE EXCEPTION 'Отзыв % не найден или не принадлежит вам.', p_id; END IF;
END; $$;

CREATE OR REPLACE FUNCTION core.user_get_reviews(p_user INT)
RETURNS SETOF core.reviews LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
BEGIN
  IF p_user IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя для получения отзывов.'; END IF;
  RETURN QUERY SELECT * FROM core.reviews WHERE id_recipient = p_user ORDER BY id_review;
END; $$;

CREATE OR REPLACE FUNCTION core.user_get_review_by_id(p_id INT)
RETURNS core.reviews LANGUAGE plpgsql STABLE SECURITY DEFINER AS $$
DECLARE result core.reviews%ROWTYPE;
BEGIN
  IF p_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор отзыва.'; END IF;
  SELECT * INTO result FROM core.reviews WHERE id_review = p_id;
  IF NOT FOUND THEN RAISE EXCEPTION 'Отзыв % не найден.', p_id; END IF;
  RETURN result;
END; $$;

CREATE OR REPLACE PROCEDURE core.user_create_complaint(
  p_performer_id INT,
  p_filed_by     INT,
  p_id_user      INT,
  p_description  TEXT,
  p_media        JSONB DEFAULT NULL,
  p_id_order     INT   DEFAULT NULL,
  p_id_order_arc INT   DEFAULT NULL
) LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_filed_by     IS NULL THEN RAISE EXCEPTION 'Не указан податель жалобы.'; END IF;
  IF p_id_user      IS NULL THEN RAISE EXCEPTION 'Не указан пользователь, на которого жалуются.'; END IF;
  IF p_description  IS NULL OR btrim(p_description) = '' THEN RAISE EXCEPTION 'Не указано описание жалобы.'; END IF;
  IF (p_id_order IS NULL AND p_id_order_arc IS NULL) THEN RAISE EXCEPTION 'Жалоба должна быть привязана к заказу или архивному заказу.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_create_complaint', false);

  INSERT INTO core.complaints(filed_by, id_user, status, description, media, id_order, id_order_arc)
  VALUES (p_filed_by, p_id_user, 'new', p_description, p_media, p_id_order, p_id_order_arc);
END; $$;

CREATE OR REPLACE PROCEDURE core.user_update_complaint (
  p_performer_id INT,
  p_id           INT,
  p_description  TEXT        DEFAULT NULL,
  p_media        JSONB       DEFAULT NULL
) LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор жалобы.'; END IF;

  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_update_complaint', false);

  UPDATE core.complaints
     SET description  = COALESCE(p_description, description),
         media        = COALESCE(p_media,       media)
   WHERE id_complaint = p_id
     AND filed_by     = p_performer_id;  -- только автор может править
  IF NOT FOUND THEN RAISE EXCEPTION 'Жалоба % не найдена или не принадлежит вам.', p_id; END IF;
END; $$;
CREATE OR REPLACE FUNCTION core.fn_complaints_limit_desc_changes()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  -- считаем изменением только реальную смену текста
  IF btrim(NEW.description) IS DISTINCT FROM btrim(OLD.description)
  	OR NEW.media IS DISTINCT FROM OLD.media THEN
    IF OLD.description_change_count >= 3 THEN
      RAISE EXCEPTION 'Описание жалобы можно менять не более 3 раз.';
    END IF;

    NEW.description_change_count := OLD.description_change_count + 1;
  END IF;

  RETURN NEW;
END;
$$;
DROP TRIGGER IF EXISTS trg_complaints_limit_desc_changes ON core.complaints;
CREATE TRIGGER trg_complaints_limit_desc_changes
BEFORE UPDATE ON core.complaints
FOR EACH ROW
EXECUTE FUNCTION core.fn_complaints_limit_desc_changes();

CREATE OR REPLACE PROCEDURE core.user_delete_complaint (
  p_performer_id INTEGER,
  p_id           INTEGER
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор жалобы.'; END IF;

  PERFORM set_config('audit.user_id',   p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_delete_complaint', false);

  BEGIN
    DELETE FROM core.complaints WHERE id_complaint = p_id;
    IF NOT FOUND THEN RAISE EXCEPTION 'Жалоба с идентификатором % не найдена.', p_id; END IF;

  EXCEPTION
    WHEN OTHERS THEN
      RAISE EXCEPTION 'Ошибка при удалении жалобы. Пожалуйста, попробуйте позже.';
  END;
END;
$$;


CREATE OR REPLACE FUNCTION core.user_get_complaints(
  user_id INTEGER
)
  RETURNS TABLE (
    "Id_Complaint" INTEGER,
    "Id_User"      INTEGER,
    "Filed_By"     INTEGER,
    "TargetName"   TEXT,
    "Status"       VARCHAR(50),
    "Description"  TEXT
  )
  LANGUAGE plpgsql
  STABLE
  SECURITY DEFINER
AS $$
BEGIN
  IF user_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;

  BEGIN
    RETURN QUERY
      SELECT
        c.id_complaint AS "Id_Complaint",
        c.id_user      AS "Id_User",
        c.filed_by     AS "Filed_By",
        COALESCE(u.first_name || ' ' || u.last_name, '[удалён]') AS "TargetName",
        c.status       AS "Status",
        c.description  AS "Description"
      FROM core.v_complaints c
      LEFT JOIN core.v_users u ON u.id_user = c.id_user
      WHERE c.filed_by = user_id
      ORDER BY c.id_complaint DESC;

  EXCEPTION
    WHEN OTHERS THEN
      RAISE EXCEPTION 'Ошибка при получении списка жалоб. Пожалуйста, попробуйте позже.';
  END;
END;
$$;


CREATE OR REPLACE PROCEDURE core.user_create_portfolio (
  p_performer_id INTEGER,
  p_user INTEGER,
  p_description TEXT,
  p_media JSONB,
  p_skills TEXT[],
  p_experience TEXT
) LANGUAGE plpgsql SECURITY DEFINER AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_user IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор владельца портфолио.'; END IF;
  IF p_description IS NULL OR btrim(p_description) = '' THEN RAISE EXCEPTION 'Не указано описание портфолио.'; END IF;
  IF p_skills IS NULL OR array_length(p_skills,1) = 0 THEN RAISE EXCEPTION 'Набор навыков не может быть пустым.'; END IF;
  IF p_experience IS NULL OR btrim(p_experience) = '' THEN RAISE EXCEPTION 'Не указан опыт работы.'; END IF;
  
  PERFORM set_config('audit.user_id', p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_create_portfolio', false);

  BEGIN
    INSERT INTO core.portfolio (id_user, description, media, skills, experience)
    VALUES (p_user, p_description, p_media, p_skills, p_experience);

  EXCEPTION
    WHEN OTHERS THEN
      RAISE EXCEPTION 'Ошибка при создании портфолио. Пожалуйста, попробуйте позже.';
  END;
END;
$$;


CREATE OR REPLACE FUNCTION core.user_get_portfolios(
  p_user INTEGER
)
  RETURNS SETOF core.portfolio
  LANGUAGE plpgsql
  STABLE
  SECURITY DEFINER
AS $$
BEGIN
  IF p_user IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;

  BEGIN
    RETURN QUERY
      SELECT * FROM core.portfolio WHERE id_user = p_user ORDER BY id_portfolio;

  EXCEPTION
    WHEN OTHERS THEN
      RAISE EXCEPTION 'Ошибка при получении портфолио. Пожалуйста, попробуйте позже.';
  END;
END;
$$;


CREATE OR REPLACE PROCEDURE core.user_update_portfolio (
  p_performer_id INTEGER,
  p_user         INTEGER,
  p_portfolio    INTEGER,
  p_description  TEXT      DEFAULT NULL,
  p_media        JSONB     DEFAULT NULL,
  p_skills       TEXT[]    DEFAULT NULL,
  p_experience   TEXT      DEFAULT NULL
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_user IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор владельца портфолио.'; END IF;
  IF p_portfolio IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор портфолио.'; END IF;

  PERFORM set_config('audit.user_id',   p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_update_portfolio', false);

  BEGIN
    UPDATE core.portfolio
       SET description = COALESCE(p_description, description),
           media       = COALESCE(p_media,       media),
           skills      = COALESCE(p_skills,      skills),
           experience  = COALESCE(p_experience,  experience)
     WHERE id_user      = p_user
       AND id_portfolio = p_portfolio;

    IF NOT FOUND THEN RAISE EXCEPTION 'Портфолио с идентификатором % не найдено для пользователя %.', p_portfolio, p_user; END IF;

  EXCEPTION
    WHEN OTHERS THEN
      RAISE EXCEPTION 'Ошибка при обновлении портфолио. Пожалуйста, попробуйте позже.';
  END;
END;
$$;


CREATE OR REPLACE PROCEDURE core.user_delete_portfolio (
  p_performer_id INTEGER,
  p_user         INTEGER,
  p_portfolio    INTEGER
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_user IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор владельца портфолио.'; END IF;
  IF p_portfolio IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор портфолио.'; END IF;

  PERFORM set_config('audit.user_id',   p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_delete_portfolio', false);

  BEGIN
    DELETE FROM core.portfolio WHERE id_user = p_user AND id_portfolio = p_portfolio;
    IF NOT FOUND THEN RAISE EXCEPTION 'Портфолио с идентификатором % не найдено для пользователя %.', p_portfolio, p_user; END IF;

  EXCEPTION
    WHEN OTHERS THEN
      RAISE EXCEPTION 'Ошибка при удалении портфолио. Пожалуйста, попробуйте позже.';
  END;
END;
$$;


CREATE OR REPLACE PROCEDURE core.user_update_profile (
  p_performer_id INTEGER,
  p_id           INTEGER,
  p_password     CHAR(128) DEFAULT NULL,
  p_last_name    VARCHAR    DEFAULT NULL,
  p_first_name   VARCHAR    DEFAULT NULL,
  p_middle_name  VARCHAR    DEFAULT NULL,
  p_gender       VARCHAR    DEFAULT NULL,
  p_phone        VARCHAR    DEFAULT NULL,
  p_email        VARCHAR    DEFAULT NULL
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор профиля.'; END IF;

  PERFORM set_config('audit.user_id',   p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_update_profile', false);

  BEGIN
    UPDATE core.users
       SET password     = COALESCE(p_password,     password),
           last_name    = COALESCE(p_last_name,    last_name),
           first_name   = COALESCE(p_first_name,   first_name),
           middle_name  = COALESCE(p_middle_name,  middle_name),
           gender       = COALESCE(p_gender,       gender),
           phone_number = COALESCE(p_phone,        phone_number),
           email        = COALESCE(p_email,        email)
     WHERE id_user = p_id;

    IF NOT FOUND THEN RAISE EXCEPTION 'Пользователь с идентификатором % не найден.', p_id; END IF;

  EXCEPTION
    WHEN OTHERS THEN
      RAISE EXCEPTION 'Ошибка при обновлении профиля. Пожалуйста, попробуйте позже.';
  END;
END;
$$;

CREATE OR REPLACE PROCEDURE core.user_update_profile (
  p_performer_id INTEGER,
  p_id           INTEGER,
  p_password     CHAR(128) DEFAULT NULL,
  p_last_name    VARCHAR    DEFAULT NULL,
  p_first_name   VARCHAR    DEFAULT NULL,
  p_middle_name  VARCHAR    DEFAULT NULL,
  p_gender       VARCHAR    DEFAULT NULL,
  p_phone        VARCHAR    DEFAULT NULL,
  p_email        VARCHAR    DEFAULT NULL,
  p_photo        bytea		DEFAULT NULL
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор профиля.'; END IF;

  PERFORM set_config('audit.user_id',   p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_update_profile', false);

  BEGIN
    UPDATE core.users
       SET password     = COALESCE(p_password,     password),
           last_name    = COALESCE(p_last_name,    last_name),
           first_name   = COALESCE(p_first_name,   first_name),
           middle_name  = COALESCE(p_middle_name,  middle_name),
           gender       = COALESCE(p_gender,       gender),
           phone_number = COALESCE(p_phone,        phone_number),
           email        = COALESCE(p_email,        email),
		   photo =
               CASE
                   WHEN p_photo IS NULL THEN photo
                   WHEN octet_length(p_photo) = 0 THEN NULL
                   ELSE p_photo
               END
     WHERE id_user = p_id;

    IF NOT FOUND THEN RAISE EXCEPTION 'Пользователь с идентификатором % не найден.', p_id; END IF;

  EXCEPTION
    WHEN OTHERS THEN
      RAISE EXCEPTION 'Ошибка при обновлении профиля. Пожалуйста, попробуйте позже.';
  END;
END;
$$;


CREATE OR REPLACE PROCEDURE core.user_send_project_invite (
  p_performer_id INTEGER,
  p_sender       INT,
  p_receiver     INT,
  p_project      INT
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_sender IS NULL THEN RAISE EXCEPTION 'Не указан отправитель приглашения.'; END IF;
  IF p_receiver IS NULL THEN RAISE EXCEPTION 'Не указан получатель приглашения.'; END IF;
  IF p_project IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор проекта.'; END IF;

  PERFORM set_config('audit.user_id',   p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_send_project_invite', false);

  IF EXISTS (SELECT 1 FROM core.orders WHERE id_project = p_project) THEN RAISE EXCEPTION 'Проект % уже занят.', p_project; END IF;

  BEGIN
    INSERT INTO core.notifications(id_sender, id_receiver, id_project)
    VALUES (p_sender, p_receiver, p_project);
  EXCEPTION
    WHEN unique_violation THEN
      RAISE EXCEPTION 'Такое приглашение уже существует.';
  END;

EXCEPTION
  WHEN OTHERS THEN
    RAISE EXCEPTION 'Ошибка при отправке приглашения. Пожалуйста, попробуйте позже.';
END;
$$;


CREATE OR REPLACE PROCEDURE core.user_accept_invite (
  p_performer_id INTEGER,
  p_notification INTEGER,
  p_freelancer   INTEGER
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
  v_row core.notifications%ROWTYPE;
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_notification IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор приглашения.';END IF;
  IF p_freelancer IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор фрилансера.'; END IF;

  PERFORM set_config('audit.user_id',         p_performer_id::text, false);
  PERFORM set_config('audit.proc_name',       'core.user_accept_invite', false);
  PERFORM set_config('audit.parent_proc_name','core.user_accept_invite', false);

  SELECT * INTO v_row FROM core.notifications WHERE id_notification = p_notification AND id_receiver = p_freelancer;
  IF NOT FOUND THEN RAISE EXCEPTION 'Приглашение с идентификатором % не найдено или не предназначено для вас.', p_notification; END IF;

  BEGIN
    CALL core.user_create_order(p_performer_id, v_row.id_project, p_freelancer, 'pending', NULL);

    DELETE FROM core.notifications WHERE id_notification = p_notification;

  EXCEPTION
    WHEN OTHERS THEN
      RAISE EXCEPTION 'Ошибка при принятии приглашения. Пожалуйста, попробуйте позже.';
  END;
END;
$$;


CREATE OR REPLACE PROCEDURE core.user_decline_invite (
  p_performer_id INTEGER,
  p_notification INTEGER,
  p_freelancer   INTEGER
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
  IF p_performer_id IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор пользователя.'; END IF;
  IF p_notification IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор приглашения.'; END IF;
  IF p_freelancer IS NULL THEN RAISE EXCEPTION 'Не указан идентификатор фрилансера.'; END IF;

  PERFORM set_config('audit.user_id',   p_performer_id::text, false);
  PERFORM set_config('audit.proc_name', 'core.user_decline_invite', false);

  BEGIN
    DELETE FROM core.notifications WHERE id_notification = p_notification AND id_receiver = p_freelancer;
    IF NOT FOUND THEN RAISE EXCEPTION 'Приглашение с идентификатором % не найдено или не принадлежит вам.', p_notification; END IF;

  EXCEPTION
    WHEN OTHERS THEN
      RAISE EXCEPTION 'Ошибка при отклонении приглашения. Пожалуйста, попробуйте позже.';
  END;
END;
$$;


CREATE OR REPLACE FUNCTION core.f_notif_cleanup() RETURNS TRIGGER
LANGUAGE plpgsql AS $$
BEGIN
  PERFORM set_config('audit.skip', 'on', true);
	
  BEGIN
    DELETE FROM core.notifications WHERE id_project = NEW.id_project;
  EXCEPTION WHEN OTHERS THEN RAISE WARNING 'Ошибка очистки уведомлений для проекта %: %', NEW.id_project, SQLERRM;
  END;
  RETURN NEW;
END;
$$;

CREATE OR REPLACE TRIGGER trg_notif_cleanup
AFTER INSERT ON core.orders
FOR EACH ROW EXECUTE FUNCTION core.f_notif_cleanup();

CREATE OR REPLACE FUNCTION core.fn_audit_trigger()
RETURNS TRIGGER
LANGUAGE plpgsql AS $$
DECLARE
  v_old      JSONB;
  v_new      JSONB;
  v_id       INT;
  v_user     INT;
  v_proc     TEXT;
  pk_column  TEXT;
BEGIN
  BEGIN
    v_user := current_setting('audit.user_id')::INT;
  EXCEPTION WHEN OTHERS THEN
    RETURN NULL;
  END;

  
  v_proc := COALESCE(
    current_setting('audit.proc_name', TRUE),
	current_setting('audit.parent_proc_name', TRUE)
  );

  IF current_setting('audit.skip', TRUE) = 'on' THEN
    RETURN NULL;
  END IF;

  v_old := CASE WHEN TG_OP = 'INSERT' THEN NULL ELSE to_jsonb(OLD) END;
  v_new := CASE WHEN TG_OP = 'DELETE' THEN NULL ELSE to_jsonb(NEW) END;

  IF TG_NARGS < 1 THEN
    RAISE EXCEPTION 'fn_audit_trigger requires 1 argument: pk_column';
  END IF;
  pk_column := TG_ARGV[0];

  v_id := COALESCE(
    (v_new ->> pk_column)::INT,
    (v_old ->> pk_column)::INT
  );

  INSERT INTO core.audit_logs (user_id,proc_name,action,table_name,record_id,old_data,new_data,changed_at)
  VALUES (v_user,v_proc,TG_OP,TG_TABLE_SCHEMA || '.' || TG_TABLE_NAME,v_id,v_old,v_new,now());

  RETURN NULL;
END;
$$;


CREATE OR REPLACE TRIGGER trg_audit_roles
  AFTER INSERT OR UPDATE OR DELETE ON core.roles
  FOR EACH ROW EXECUTE FUNCTION core.fn_audit_trigger('id_role');

CREATE OR REPLACE TRIGGER trg_audit_users
  AFTER INSERT OR UPDATE OR DELETE ON core.users
  FOR EACH ROW EXECUTE FUNCTION core.fn_audit_trigger('id_user');

CREATE OR REPLACE TRIGGER trg_audit_projects
  AFTER INSERT OR UPDATE OR DELETE ON core.projects
  FOR EACH ROW EXECUTE FUNCTION core.fn_audit_trigger('id_project');

CREATE OR REPLACE TRIGGER trg_audit_reviews
  AFTER INSERT OR UPDATE OR DELETE ON core.reviews
  FOR EACH ROW EXECUTE FUNCTION core.fn_audit_trigger('id_review');

CREATE OR REPLACE TRIGGER trg_audit_orders
  AFTER INSERT OR UPDATE OR DELETE ON core.orders
  FOR EACH ROW EXECUTE FUNCTION core.fn_audit_trigger('id_order');

CREATE OR REPLACE TRIGGER trg_audit_orders_archive
  AFTER INSERT OR UPDATE OR DELETE ON core.orders_archive
  FOR EACH ROW EXECUTE FUNCTION core.fn_audit_trigger('id_order_arc');

CREATE OR REPLACE TRIGGER trg_audit_complaints
  AFTER INSERT OR UPDATE OR DELETE ON core.complaints
  FOR EACH ROW EXECUTE FUNCTION core.fn_audit_trigger('id_complaint');

CREATE OR REPLACE TRIGGER trg_audit_warnings
  AFTER INSERT OR UPDATE OR DELETE ON core.warnings
  FOR EACH ROW EXECUTE FUNCTION core.fn_audit_trigger('id_warning');

CREATE OR REPLACE TRIGGER trg_audit_portfolio
  AFTER INSERT OR UPDATE OR DELETE ON core.portfolio
  FOR EACH ROW EXECUTE FUNCTION core.fn_audit_trigger('id_portfolio');

CREATE OR REPLACE TRIGGER trg_audit_notifications
  AFTER INSERT OR UPDATE OR DELETE ON core.notifications
  FOR EACH ROW EXECUTE FUNCTION core.fn_audit_trigger('id_notification');



REVOKE ALL ON SCHEMA core FROM PUBLIC;
REVOKE ALL ON SCHEMA core FROM svc_app, svc_mod, svc_user, app_mod_usr, app_end_usr;

GRANT CONNECT ON DATABASE freelance_app TO svc_app;
GRANT USAGE ON SCHEMA core TO svc_app;
GRANT SELECT ON CORE.USERS TO SVC_APP;
GRANT SELECT ON CORE.ROLES TO SVC_APP;
GRANT INSERT ON CORE.USERS TO SVC_APP;
GRANT USAGE, SELECT ON SEQUENCE core.users_id_user_seq TO SVC_APP;

GRANT svc_mod TO app_mod_usr;
GRANT svc_user TO app_end_usr;

GRANT CONNECT, TEMPORARY ON DATABASE freelance_app TO svc_admin;
GRANT USAGE, CREATE ON SCHEMA core TO svc_admin;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA core TO svc_admin;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA core TO svc_admin;
GRANT ALL PRIVILEGES ON ALL FUNCTIONS IN SCHEMA core TO svc_admin;
GRANT ALL PRIVILEGES ON ALL PROCEDURES IN SCHEMA core TO svc_admin;
ALTER DEFAULT PRIVILEGES IN SCHEMA core GRANT ALL ON TABLES TO svc_admin;
ALTER DEFAULT PRIVILEGES IN SCHEMA core GRANT ALL ON SEQUENCES TO svc_admin;
ALTER DEFAULT PRIVILEGES IN SCHEMA core GRANT ALL ON FUNCTIONS TO svc_admin;
REVOKE CONNECT ON DATABASE postgres    FROM svc_admin;
REVOKE CONNECT ON DATABASE template1   FROM svc_admin;
REVOKE CONNECT ON DATABASE template0   FROM svc_admin;

GRANT CONNECT ON DATABASE freelance_app TO svc_mod;
GRANT USAGE ON SCHEMA core TO svc_mod;
GRANT EXECUTE ON FUNCTION core.mod_get_complaints(text) TO svc_mod;
GRANT EXECUTE ON PROCEDURE core.mod_update_complaint_status(INTEGER, INTEGER, VARCHAR, INTEGER) TO svc_mod;
GRANT EXECUTE ON PROCEDURE core.mod_resolve_complaint(integer, integer, varchar, integer) TO svc_mod;
GRANT EXECUTE ON PROCEDURE core.mod_issue_warning(INT,INT,INT,TEXT,INT) TO svc_mod;
GRANT EXECUTE ON PROCEDURE core.update_user_last_online(integer, integer) TO svc_mod;

GRANT SELECT ON core.v_roles TO svc_mod;
GRANT SELECT ON core.v_users TO svc_mod;
GRANT SELECT ON core.v_projects TO svc_mod;
GRANT SELECT ON core.v_reviews TO svc_mod;
GRANT SELECT ON core.v_orders TO svc_mod;
GRANT SELECT ON core.v_order_extended TO svc_mod;
GRANT SELECT ON core.v_order_archive_extended TO svc_mod;
GRANT SELECT ON core.v_portfolio TO svc_mod;
GRANT SELECT ON core.v_mod_users TO svc_mod;
GRANT SELECT ON core.v_mod_complaints TO svc_mod;

GRANT CONNECT ON DATABASE freelance_app TO svc_user;
GRANT USAGE ON SCHEMA core TO svc_user;
GRANT EXECUTE ON FUNCTION core.user_get_users() TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_update_profile (integer, integer, CHAR(128), VARCHAR, VARCHAR, VARCHAR, VARCHAR, VARCHAR, VARCHAR) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_update_profile (integer, integer, CHAR(128), VARCHAR, VARCHAR, VARCHAR, VARCHAR, VARCHAR, VARCHAR, BYTEA) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.update_user_last_online(integer, integer) TO svc_user;

GRANT SELECT ON core.v_roles TO svc_user;
GRANT SELECT ON core.v_users TO svc_user;
GRANT SELECT ON core.v_projects TO svc_user;
GRANT SELECT ON core.v_complaints TO svc_user;
GRANT SELECT ON core.v_all_counterparts TO svc_user;
GRANT SELECT ON core.v_reviews TO svc_user;
GRANT SELECT ON core.v_orders_reviews TO svc_user;
GRANT SELECT ON core.v_orders TO svc_user;
GRANT SELECT ON core.v_order_extended TO svc_user;
GRANT SELECT ON core.v_orders_archive TO svc_user;
GRANT SELECT ON core.v_order_archive_extended TO svc_user;
GRANT SELECT ON core.v_portfolio TO svc_user;
GRANT SELECT ON core.v_user_notifications TO svc_user;
GRANT SELECT ON core.v_user_warnings TO svc_user;

GRANT EXECUTE ON PROCEDURE core.user_create_project TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_update_project TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_delete_project(integer, integer) TO svc_user;
GRANT EXECUTE ON FUNCTION core.user_get_projects_by_customer(integer) TO svc_user;
GRANT EXECUTE ON FUNCTION core.user_get_projects() TO svc_user;
GRANT EXECUTE ON FUNCTION core.user_get_projects_by_status(varchar(50)) TO svc_user;
GRANT EXECUTE ON FUNCTION core.user_get_project_by_id(integer) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_create_order(integer, integer, integer, varchar, date) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_update_order(integer, integer, varchar, date) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_delete_order(integer, integer) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_archive_order(integer, integer) TO svc_user;
GRANT EXECUTE ON FUNCTION core.user_get_orders_by_customer(integer) TO svc_user;
GRANT EXECUTE ON FUNCTION core.user_get_orders_by_freelancer(integer) TO svc_user;
GRANT EXECUTE ON FUNCTION core.user_get_orders() TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_create_review(integer, integer, integer, text, integer, jsonb) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_update_review(integer, integer, integer, text, integer, jsonb) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_delete_review(integer, integer, integer) TO svc_user;
GRANT EXECUTE ON FUNCTION core.user_get_reviews(integer) TO svc_user;
GRANT EXECUTE ON FUNCTION core.user_get_review_by_id(integer) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_create_complaint(integer, integer, integer, text, jsonb) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_update_complaint(integer, integer, varchar, integer, integer, text) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_delete_complaint(integer, integer) TO svc_user;
GRANT EXECUTE ON FUNCTION core.user_get_complaints(integer) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_create_portfolio(integer, integer, text, jsonb, text[], text) TO svc_user;
GRANT EXECUTE ON FUNCTION core.user_get_portfolios(integer) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_update_portfolio(integer, integer, integer, text, jsonb, text[], text) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_delete_portfolio(integer, integer, integer) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_send_project_invite (integer, integer, integer, integer) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_accept_invite (integer, integer, integer) TO svc_user;
GRANT EXECUTE ON PROCEDURE core.user_decline_invite(integer, integer, integer) TO svc_user;