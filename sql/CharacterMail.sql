--
-- Character Mail Schema
--

-- Table: webapi."MailMessages"
CREATE TABLE webapi."MailMessages" (
    id bigserial PRIMARY KEY,
    character_guid bigint NOT NULL REFERENCES webapi."Characters"(character_guid) ON DELETE CASCADE,
    sender_guid bigint NOT NULL DEFAULT 0,
    sender_name text NOT NULL,
    subject text NOT NULL,
    body text NOT NULL,
    is_read boolean NOT NULL DEFAULT FALSE,
    mail_type text NOT NULL DEFAULT 'system_message',
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    expires_at timestamp with time zone
);

CREATE INDEX mailmessages_character_guid_idx ON webapi."MailMessages" (character_guid);

-- Table: webapi."MailAttachments"
CREATE TABLE webapi."MailAttachments" (
    id bigserial PRIMARY KEY,
    message_id bigint NOT NULL REFERENCES webapi."MailMessages"(id) ON DELETE CASCADE,
    item_sdb_id integer NOT NULL,
    item_guid bigint, -- For unique items that already have a GUID
    quantity integer NOT NULL DEFAULT 1,
    claimed boolean NOT NULL DEFAULT FALSE
);

CREATE INDEX mailattachments_message_id_idx ON webapi."MailAttachments" (message_id);

--
-- Name: SendMail(bigint, bigint, text, text, text, text); Type: FUNCTION; Schema: webapi;
--
CREATE OR REPLACE FUNCTION webapi."SendMail"(
    p_character_guid bigint,
    p_sender_guid bigint,
    p_sender_name text,
    p_subject text,
    p_body text,
    p_mail_type text DEFAULT 'system_message'
) RETURNS bigint
    LANGUAGE plpgsql
AS $$
DECLARE
    v_mail_id bigint;
BEGIN
    INSERT INTO webapi."MailMessages" (character_guid, sender_guid, sender_name, subject, body, mail_type)
    VALUES (p_character_guid, p_sender_guid, p_sender_name, p_subject, p_body, p_mail_type)
    RETURNING id INTO v_mail_id;

    RETURN v_mail_id;
END;
$$;

--
-- Name: AddMailAttachment(bigint, integer, bigint, integer); Type: FUNCTION; Schema: webapi;
--
CREATE OR REPLACE FUNCTION webapi."AddMailAttachment"(
    p_message_id bigint,
    p_item_sdb_id integer,
    p_item_guid bigint DEFAULT NULL,
    p_quantity integer DEFAULT 1
) RETURNS void
    LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO webapi."MailAttachments" (message_id, item_sdb_id, item_guid, quantity)
    VALUES (p_message_id, p_item_sdb_id, p_item_guid, p_quantity);
END;
$$;
