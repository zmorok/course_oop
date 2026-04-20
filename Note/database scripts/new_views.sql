/* DROPS */
DROP VIEW IF EXISTS core.v_orders_reviews;
DROP VIEW IF EXISTS core.v_order_archive_extended;
DROP VIEW IF EXISTS core.v_orders_archive;
DROP VIEW IF EXISTS core.v_order_extended;
DROP VIEW IF EXISTS core.v_orders;
DROP VIEW IF EXISTS core.v_reviews;
DROP VIEW IF EXISTS core.v_projects;
DROP VIEW IF EXISTS core.v_users;
DROP VIEW IF EXISTS core.v_roles;
DROP VIEW IF EXISTS core.v_portfolio;
DROP VIEW IF EXISTS core.v_user_notifications;
DROP VIEW IF EXISTS core.v_user_warnings;
DROP VIEW IF EXISTS core.v_all_counterparts;
DROP VIEW IF EXISTS core.v_complaints;

/* роли */
CREATE OR REPLACE VIEW core.v_roles AS
SELECT r.id_role, r.role_name
FROM core.roles r;

/* пользователи */
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

/* проекты */
CREATE OR REPLACE VIEW core.v_projects AS
SELECT
  p.id_project,
  p.id_customer,
  p.title,
  p.status,
  p.description,
  p.media
FROM core.projects p;

/* отзывы по архивным заказам */
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

/* активные заказы */
CREATE OR REPLACE VIEW core.v_orders AS
SELECT
  o.id_order,
  o.id_project,
  o.id_freelancer,
  o.status,
  o.creation_date,
  o.deadline
FROM core.orders o;

/* заказы - расширенные */
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

/* архив заказов */
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

/* архив заказов - расширенный */
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

/* портфолио */
CREATE OR REPLACE VIEW core.v_portfolio AS
SELECT
  pf.id_portfolio,
  pf.id_user,
  pf.description,
  pf.media,
  pf.skills,
  pf.experience
FROM core.portfolio pf;

/* архивный заказ + оба отзыва */
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

/* пара "пользователь + контрагент" по архивным заказам */
CREATE OR REPLACE VIEW core.v_all_counterparts AS
SELECT DISTINCT
  u.id_user                         AS user_id,
  CASE
    WHEN oa.id_customer = u.id_user   THEN oa.id_freelancer
    ELSE oa.id_customer
  END                               AS counterpart_id
FROM core.orders_archive oa
JOIN core.users u ON u.id_user IN (oa.id_customer, oa.id_freelancer);

/* жалобы */
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

/* уведомления */
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

/* предупреждения пользователя */
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

/* пользователи для администратора */
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

/* жалобы для администратора */
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